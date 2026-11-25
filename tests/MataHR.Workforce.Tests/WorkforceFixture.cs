using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MataHR.Persistence;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace MataHR.Workforce.Tests;
public class WorkforceFixture : IAsyncLifetime
{
    public static class Networking
    {
        [ModuleInitializer]
        public static void Init() => AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
    }

    public static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
    public DateTimeOffset Now => FixedNow;
    public CaptureRecoveryDelivery Recovery { get; } = new();

    public const string Password = "Fictional-Test-Account42!";
    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();
    public Worker Manager { get; private set; } = null!;
    public Worker Alice { get; private set; } = null!;
    public Worker Outsider { get; private set; } = null!;
    public Worker Bob { get; private set; } = null!;
    public AbsencePolicy Policy { get; private set; } = null!;
    public ClockLocation Location { get; private set; } = null!;
    public WebApplicationFactory<WorkforceProgram> Factory { get; private set; } = null!;
    public string Connection { get; private set; } = "";
    public ErrorLogs Logs { get; } = new();

    readonly Dictionary<string, string> cookies = new();
    readonly string database = "MataHR_Workforce_Test_" + Guid.NewGuid().ToString("N");
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("MATAHR_TEST_SQL") ?? throw new InvalidOperationException("Run scripts/Test-Workforce.ps1 against local SQL Server.");
        Connection = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = database
        }.ConnectionString;
        Factory = new WebApplicationFactory<WorkforceProgram>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedClock()));
                services.Replace(ServiceDescriptor.Singleton<IAccountRecoveryDelivery>(Recovery));
                services.AddSingleton<IStartupFilter, TestClientNetwork>();
            });
            b.ConfigureLogging(l => l.AddProvider(Logs));
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { { "ConnectionStrings:Default", Connection }, { "Bootstrap:Email", "" }, { "Bootstrap:Password", "" } }));
        });
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(source) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = "CREATE DATABASE [" + database + "]";
            await create.ExecuteNonQueryAsync();
        }

        await using (var existing = new SqlConnection(Connection))
        {
            await existing.OpenAsync();
            await using var sentinel = existing.CreateCommand();
            sentinel.CommandText = "CREATE TABLE LegacySentinel(Id int NOT NULL PRIMARY KEY, Value nvarchar(40) NOT NULL); INSERT INTO LegacySentinel VALUES(1,N'Preserved record');";
            await sentinel.ExecuteNonQueryAsync();
        }

        using var client = Factory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
        Worker Person(Guid tenant, string number, string name) => new(tenant, number, name, name.ToLowerInvariant() + "@example.invalid", new DateOnly(2020, 1, 1));
        Manager = Person(TenantA, "MGR", "Manager");
        Alice = Person(TenantA, "EMP", "Alice");
        Alice.Assign(null, Manager.Id);
        Outsider = Person(TenantA, "OTHER", "Outsider");
        Bob = Person(TenantB, "EMP", "Bob");
        var hr = Person(TenantA, "HR1", "HROne");
        var hr2 = Person(TenantA, "HR2", "HRTwo");
        db.AddRange(Manager, Alice, Outsider, Bob, hr, hr2);
        await db.SaveChangesAsync();
        var tenancy = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var unitManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        async Task Account(Guid tenant, string email, string role, Guid? employee = null)
        {
            using (tenancy.Change(tenant))
            {
                using var unit = unitManager.Begin(requiresNew: true, isTransactional: true);
                await scope.ServiceProvider.GetRequiredService<WorkforcePermissionSeeder>().SeedAsync(tenant);
                var manager = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
                var account = new Volo.Abp.Identity.IdentityUser(Guid.NewGuid(), email + "@example.invalid", email + "@example.invalid", tenant);
                account.SetProperty("EmployeeId", employee);
                var created = await manager.CreateAsync(account, Password);
                Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(x => x.Description)));
                Assert.True((await manager.AddToRoleAsync(account, role)).Succeeded);
                await unit.CompleteAsync();
            }
        }

        await Account(TenantA, "admin", "Admin");
        await Account(TenantA, "hr", "HR", hr.Id);
        await Account(TenantA, "hr2", "HR", hr2.Id);
        await Account(TenantA, "manager", "Manager", Manager.Id);
        await Account(TenantA, "alice", "Employee", Alice.Id);
        await Account(TenantA, "outsider", "Employee", Outsider.Id);
        await Account(TenantA, "privacy", "Employee", Outsider.Id);
        await Account(TenantA, "lockout", "Employee", Outsider.Id);
        await Account(TenantB, "foreign", "HR", Bob.Id);
        Location = new ClockLocation
        {
            TenantId = TenantA,
            Name = "Office",
            Kind = "Office"
        };
        Policy = new AbsencePolicy(TenantA, "Annual", 1, true, true, 30, false, 0, 0, 0, true);
        db.AddRange(Location, Policy);
        foreach (var e in new[]
        {
            Manager,
            Alice,
            Outsider,
            hr,
            hr2
        }

        )
        {
            db.Add(new Compensation(TenantA, e.Id, new DateOnly(2020, 1, 1), 15000, 0, 0, 100, 1.5m));
            var allocation = new LeaveAllocation(TenantA, e.Id, Policy.Id, Now.Year);
            allocation.SetAmounts(30, 0, 0, 0, Guid.NewGuid(), "Initial test allocation");
            db.Add(allocation);
        }

        await db.SaveChangesAsync();
    }

    int clientCounter;
    public HttpClient NewClient()
    {
        var c = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-MataHR-Request", "1");
        c.DefaultRequestHeaders.Add("X-Test-Client-IP", "192.0.2." + Interlocked.Increment(ref clientCounter));
        return c;
    }

    public async Task<HttpClient> Client(string email, bool acknowledge = true)
    {
        var c = NewClient();
        if (cookies.TryGetValue(email, out var cookie))
            c.DefaultRequestHeaders.Add("Cookie", cookie);
        else
        {
            var response = await c.PostAsJsonAsync("/api/auth/login", new LoginInput(email == "foreign" ? TenantB : TenantA, email + "@example.invalid", Password));
            Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + Logs.Text);
            cookies[email] = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(x => x.Split(';')[0]));
        }

        if (acknowledge)
        {
            var response = await c.PostAsJsonAsync("/api/auth/privacy", new { });
            Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + Logs.Text);
        }

        return c;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^MataHR_Workforce_Test_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Unsafe cleanup target.");
        var options = new DbContextOptionsBuilder<WorkforceDbContext>().UseSqlServer(Connection).Options;
        await using var db = new WorkforceDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}

public class ErrorLogs : ILoggerProvider
{
    readonly System.Collections.Concurrent.ConcurrentQueue<string> entries = new();
    public string Text => string.Join("\n", entries.TakeLast(5));

    public ILogger CreateLogger(string name) => new Capture(entries);
    public void Dispose()
    {
    }

    class Capture(System.Collections.Concurrent.ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        {
            if (IsEnabled(level))
                entries.Enqueue(format(state, error) + "\n" + error);
        }
    }
}

public class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => WorkforceFixture.FixedNow;
}

public class CaptureRecoveryDelivery : IAccountRecoveryDelivery
{
    public bool IsConfigured => true;
    public List<(Guid Tenant, string Email, string Token)> Messages { get; } = [];

    public Task SendAsync(Guid tenant, string email, string token)
    {
        Messages.Add((tenant, email, token));
        return Task.CompletedTask;
    }
}

public class TestClientNetwork : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, following) =>
        {
            if (IPAddress.TryParse(context.Request.Headers["X-Test-Client-IP"].ToString(), out var ip))
                context.Connection.RemoteIpAddress = ip;
            return following();
        });
        next(app);
    };
}
