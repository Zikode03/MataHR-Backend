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
namespace MataHR.Workforce.Tests;

public class WorkforceFixture : IAsyncLifetime
{
    public static class Networking
    {
        [ModuleInitializer] public static void Init() => AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
    }
    public const string Password = "Fictional-Test-Account42!";
    public Guid TenantA { get; } = Guid.NewGuid(); public Guid TenantB { get; } = Guid.NewGuid();
    public Worker Manager { get; private set; } = null!; public Worker Alice { get; private set; } = null!; public Worker Outsider { get; private set; } = null!; public Worker Bob { get; private set; } = null!;
    public AbsencePolicy Policy { get; private set; } = null!; public ClockLocation Location { get; private set; } = null!;
    public WebApplicationFactory<WorkforceProgram> Factory { get; private set; } = null!;
    public string Connection { get; private set; } = "";
    public ErrorLogs Logs { get; } = new();
    readonly Dictionary<string, string> cookies = new();
    readonly string database = "MataHR_Workforce_Test_" + Guid.NewGuid().ToString("N");
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("MATAHR_TEST_SQL") ?? throw new InvalidOperationException("Run scripts/Test-Workforce.ps1 against local SQL Server.");
        Connection = new SqlConnectionStringBuilder(source) { InitialCatalog = database }.ConnectionString;
        Factory = new WebApplicationFactory<WorkforceProgram>().WithWebHostBuilder(b => { b.UseEnvironment("Testing"); b.ConfigureLogging(l => l.AddProvider(Logs)); b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { { "ConnectionStrings:Default", Connection }, { "Bootstrap:Email", "" }, { "Bootstrap:Password", "" } })); });
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
        Worker Person(Guid tenant, string number, string name) => new()
        {
            TenantId = tenant,
            Number = number,
            Name = name,
            Email = name.ToLowerInvariant() + "@example.invalid",
            Started = new DateOnly(2020, 1, 1)
        };
        Manager = Person(TenantA, "MGR", "Manager");
        Alice = Person(TenantA, "EMP", "Alice");
        Alice.ManagerId = Manager.Id;
        Outsider = Person(TenantA, "OTHER", "Outsider");
        Bob = Person(TenantB, "EMP", "Bob");
        var hr = Person(TenantA, "HR1", "HROne");
        var hr2 = Person(TenantA, "HR2", "HRTwo");
        db.AddRange(Manager, Alice, Outsider, Bob, hr, hr2);
        void Account(Guid tenant, string email, string roles, Guid? employee = null)
        {
            var a = new WorkforceUser { TenantId = tenant, Email = email + "@example.invalid", Roles = roles, EmployeeId = employee };
            a.PasswordHash = new PasswordHasher<WorkforceUser>().HashPassword(a, Password);
            db.Add(a);
        }
        Account(TenantA, "admin", "Admin");
        Account(TenantA, "hr", "HR", hr.Id);
        Account(TenantA, "hr2", "HR", hr2.Id);
        Account(TenantA, "manager", "Manager", Manager.Id);
        Account(TenantA, "alice", "Employee", Alice.Id);
        Account(TenantA, "outsider", "Employee", Outsider.Id);
        Account(TenantA, "privacy", "Employee", Outsider.Id);
        Account(TenantA, "lockout", "Employee", Outsider.Id);
        Account(TenantB, "foreign", "HR", Bob.Id);
        Location = new ClockLocation { TenantId = TenantA, Name = "Office", Kind = "Office" };
        Policy = new AbsencePolicy { TenantId = TenantA, Name = "Annual", AnnualDays = 30, Paid = true, RequiresHr = true };
        db.AddRange(Location, Policy);
        foreach (var e in new[] { Manager, Alice, Outsider, hr, hr2 })
        {
            db.Add(new Compensation { TenantId = TenantA, EmployeeId = e.Id, Basic = 15000, HourlyRate = 100 });
            db.Add(new LeaveAllocation { TenantId = TenantA, EmployeeId = e.Id, PolicyId = Policy.Id, Year = DateTime.UtcNow.AddHours(2).Year, Allocation = 30 });
        }
        await db.SaveChangesAsync();
    }
    public async Task<HttpClient> Client(string email, bool acknowledge = true)
    {
        var c = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-MataHR-Request", "1");
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
            var response = await c.PostAsJsonAsync("/api/auth/privacy", new
            {
            });
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
    readonly System.Collections.Concurrent.ConcurrentQueue<string> entries = new(); public string Text => string.Join("\n", entries.TakeLast(5));
    public ILogger CreateLogger(string name) => new Capture(entries); public void Dispose()
    {
    }
    class Capture(System.Collections.Concurrent.ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        {
            if (IsEnabled(level))
                entries.Enqueue(format(state, error) + "\n" + error);
        }
    }
}
