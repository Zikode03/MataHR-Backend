using MataHR.Attendance;
using MataHR.Common;
using MataHR.Imports;
using MataHR.Accounts.Contracts;
using MataHR.Attendance.Contracts;
using MataHR.Authentication.Contracts;
using MataHR.Common.Contracts;
using MataHR.Employees.Contracts;
using MataHR.Imports.Contracts;
using MataHR.Organisation.Contracts;
using MataHR.Permissions;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Autofac;
using Volo.Abp;
using Volo.Abp.Uow;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MataHR.Persistence;
using Xunit;

namespace MataHR.IntegrationTests;
public class ConnectedWorkspaceTests(MataHrApiFixture f) : IClassFixture<MataHrApiFixture>
{
    async Task<T> Post<T>(HttpClient client, string path, object input)
    {
        var response = await client.PostAsJsonAsync(path, input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync() + f.Logs.Text);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task InitialMigrationMatchesTheCleanSqlModel()
    {
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MataHrDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("MataEmployee", db.Model.FindEntityType(typeof(MataHR.Employees.Employee))!.GetTableName());
        Assert.Equal("MataBranch", db.Model.FindEntityType(typeof(MataHR.Organisation.Branch))!.GetTableName());
        Assert.Equal("MataDepartment", db.Model.FindEntityType(typeof(MataHR.Organisation.Department))!.GetTableName());
        Assert.Equal("MataPosition", db.Model.FindEntityType(typeof(MataHR.Organisation.Position))!.GetTableName());
        Assert.Equal("MataLeaveRequest", db.Model.FindEntityType(typeof(MataHR.Leave.LeaveRequest))!.GetTableName());
        Assert.Equal("MataLeavePolicy", db.Model.FindEntityType(typeof(MataHR.Leave.LeavePolicy))!.GetTableName());
        Assert.Equal("MataEmploymentContract", db.Model.FindEntityType(typeof(MataHR.Employees.EmploymentContract))!.GetTableName());

        // Read a persisted employee through the new SQL table mapping.
        var stored = await db.Set<MataHR.Employees.Employee>()
            .FromSqlInterpolated($"SELECT * FROM dbo.MataEmployee WHERE Id = {f.Alice.Id}")
            .IgnoreQueryFilters()
            .SingleAsync();
        Assert.Equal(f.Alice.Number, stored.Number);
    }

    [Fact]
    public async Task MigratorSeedsFirstAdministratorWithoutResettingExistingGrants()
    {
        var tenantId = Guid.NewGuid();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = f.Connection,
                ["Bootstrap:Email"] = "migration-test@example.invalid",
                ["Bootstrap:Password"] = MataHrApiFixture.Password
            }).Build();

        using var application = await Volo.Abp.AbpApplicationFactory.CreateAsync<MataHR.DbMigrator.MataHrDbMigratorModule>(options =>
        {
            options.UseAutofac();
            options.Services.ReplaceConfiguration(configuration);
        });
        await application.InitializeAsync();
        try
        {
            using var scope = application.ServiceProvider.CreateScope();
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(tenantId);
            var units = services.GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
            var seeder = services.GetRequiredService<Volo.Abp.Data.IDataSeeder>();
            var context = new Volo.Abp.Data.DataSeedContext(tenantId);
            var users = services.GetRequiredService<Volo.Abp.Identity.IdentityUserManager>();
            var grants = services.GetRequiredService<Volo.Abp.PermissionManagement.IPermissionManager>();

            using (var unit = units.Begin(requiresNew: true, isTransactional: true))
            {
                await seeder.SeedAsync(context);
                await unit.CompleteAsync();
            }
            Guid originalId;
            using (var unit = units.Begin(requiresNew: true, isTransactional: true))
            {
                var user = await users.FindByEmailAsync("migration-test@example.invalid");
                Assert.NotNull(user);
                originalId = user.Id;
                Assert.Contains("Admin", await users.GetRolesAsync(user));
                Assert.True(await users.CheckPasswordAsync(user, MataHrApiFixture.Password));
                await grants.SetAsync(WorkforcePermissions.Dashboard, "R", "Admin", false);
                await unit.CompleteAsync();
            }
            using (var unit = units.Begin(requiresNew: true, isTransactional: true))
            {
                await seeder.SeedAsync(context);
                await unit.CompleteAsync();
            }
            using (var unit = units.Begin(requiresNew: true))
            {
                Assert.Equal(originalId, (await users.FindByEmailAsync("migration-test@example.invalid"))!.Id);
                Assert.False((await grants.GetAsync(WorkforcePermissions.Dashboard, "R", "Admin")).IsGranted);
                Assert.False((await grants.GetAsync(WorkforcePermissions.PayrollManage, "R", "Admin")).IsGranted);
                await unit.CompleteAsync();
            }
        }
        finally
        {
            await application.ShutdownAsync();
        }
    }

    [Fact]
    public async Task SixCharacterPasswordsWorkForCreationAndChangeWhileComplexityIsRequired()
    {
        using var admin = await f.Client("admin");
        var email = "password-policy-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        foreach (var invalid in new[] { "Ab1!x", "abcdef", "ab1!xy", "AB1!XY", "Abc!xy", "Ab12xy" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/accounts", new AccountInput(email, invalid, ["HR"], null))).StatusCode);
        await Post<AccountDto>(admin, "/api/admin/accounts", new AccountInput(email, "Ax7!qp", ["HR"], null));
        using var user = f.NewClient();
        (await user.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, "Ax7!qp"))).EnsureSuccessStatusCode();
        (await user.PostAsJsonAsync("/api/auth/privacy", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/auth/password", new PasswordChangeInput("Ax7!qp", "abcdef"))).StatusCode);
        (await user.PostAsJsonAsync("/api/auth/password", new PasswordChangeInput("Ax7!qp", "Zr8#vt"))).EnsureSuccessStatusCode();
        using var fresh = f.NewClient();
        (await fresh.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, "Zr8#vt"))).EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task AdministratorCanEditEmailAndToggleAccessWithConcurrencyAndSessionProtection()
    {
        using var admin = await f.Client("admin");
        using var employee = await f.Client("alice");
        var original = "managed-" + Guid.NewGuid().ToString("N");
        var renamed = "renamed-" + Guid.NewGuid().ToString("N");
        var account = await Post<AccountDto>(admin, "/api/admin/accounts", new AccountInput(original + "@example.invalid", MataHrApiFixture.Password, ["HR"], null));
        using var previousSession = await f.Client(original);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(["HR"], null, true, account.ConcurrencyStamp, "admin@example.invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(["HR"], null, true, account.ConcurrencyStamp))).StatusCode);
        var response = await admin.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(["HR"], f.Alice.Id, true, account.ConcurrencyStamp, renamed + "@example.invalid"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync() + f.Logs.Text);
        var changed = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal(renamed + "@example.invalid", changed.Email);
        Assert.Equal(f.Alice.Id, changed.EmployeeId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await previousSession.GetAsync("/api/auth/me")).StatusCode);
        using var currentSession = await f.Client(renamed);
        response = await admin.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(changed.Roles, changed.EmployeeId, false, changed.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var inactive = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.False(inactive.Active);
        Assert.Equal(HttpStatusCode.Unauthorized, (await currentSession.GetAsync("/api/auth/me")).StatusCode);
        using var probe = f.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await probe.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, changed.Email, MataHrApiFixture.Password))).StatusCode);
        response = await admin.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(inactive.Roles, inactive.EmployeeId, true, inactive.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.True((await response.Content.ReadFromJsonAsync<AccountDto>())!.Active);
        Assert.Equal(HttpStatusCode.OK, (await probe.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, changed.Email, MataHrApiFixture.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(["HR"], null, false, account.ConcurrencyStamp))).StatusCode);
        var own = (await admin.GetFromJsonAsync<Page<AccountDto>>("/api/admin/accounts?maxResultCount=200"))!.Items.Single(x => x.Email == "admin@example.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/api/admin/accounts/" + own.Id, new AccountUpdateInput(own.Roles, own.EmployeeId, false, own.ConcurrencyStamp))).StatusCode);
    }
    [Fact]
    public async Task AdministratorOverviewIsScopedAndRequiresAccountPermission()
    {
        using var admin = await f.Client("admin");
        using var employee = await f.Client("alice");
        using var foreign = await f.Client("foreign");
        var overview = await admin.GetFromJsonAsync<AdminOverviewDto>("/api/admin/overview");
        Assert.NotNull(overview);
        Assert.Equal(overview.TotalAccounts, overview.ActiveAccounts + overview.InactiveAccounts);
        Assert.True(overview.Roles.Single(x => x.Role == "Admin").Count > 0);
        var accounts = await admin.GetFromJsonAsync<Page<AccountDto>>("/api/admin/accounts?maxResultCount=1");
        Assert.Equal(accounts!.TotalCount, overview.TotalAccounts);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/admin/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await foreign.GetAsync("/api/admin/overview")).StatusCode);
    }
    [Fact]
    public async Task PresenceRequiresFreshCodeAccurateGeofenceAndPreventsReplay()
    {
        using var hr = await f.Client("hr");
        using var employee = await f.Client("alice");
        using var manager = await f.Client("manager");
        using var foreign = await f.Client("foreign");
        var site = await Post<LocationDto>(hr, "/api/attendance/locations", new LocationInput("Verified worksite", "Office"));
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/attendance/locations/" + site.Id + "/code", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PostAsJsonAsync("/api/attendance/locations/" + site.Id + "/code", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync("/api/attendance/clock-in", new ClockInput(site.Id))).StatusCode);
        (await hr.PutAsJsonAsync("/api/attendance/locations/" + site.Id + "/boundary", new SiteBoundaryInput(-29.78, 30.77, 150))).EnsureSuccessStatusCode();
        var first = await Post<PresenceCodeDto>(hr, "/api/attendance/locations/" + site.Id + "/code", new { });
        var latest = await Post<PresenceCodeDto>(hr, "/api/attendance/locations/" + site.Id + "/code", new { });
        ClockInput Proof(string code, double lat = -29.78, double accuracy = 10, DateTimeOffset? at = null) => new(site.Id, code, lat, 30.77, accuracy, at ?? f.Now);
        foreach (var invalid in new[]
        {
            Proof(first.Code),
            Proof(latest.Code, -28),
            Proof(latest.Code, accuracy: 100),
            Proof(latest.Code, at: f.Now.AddMinutes(-5))
        }

        )
            Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync("/api/attendance/clock-in", invalid)).StatusCode);
        var row = await Post<SessionDto>(employee, "/api/attendance/clock-in", Proof(latest.Code));
        Assert.Equal("On-site code + GPS checked", row.Evidence);
        Assert.Equal(0, row.DistanceMeters);
        Assert.Equal(10, row.AccuracyMeters);
        var current = await employee.GetFromJsonAsync<SessionDto>("/api/attendance/current");
        Assert.Equal(row.Id, current!.Id);
        await Post<SessionDto>(employee, "/api/attendance/clock-out", new { });
        Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync("/api/attendance/clock-in", Proof(latest.Code))).StatusCode);
    }

    [Fact]
    public async Task PresenceExceptionsRemainUnverifiedAndNeedIndependentReview()
    {
        using var hr = await f.Client("hr");
        using var employee = await f.Client("outsider");
        var site = await Post<LocationDto>(hr, "/api/attendance/locations", new LocationInput("Exception worksite", "Office"));
        var row = await Post<SessionDto>(employee, "/api/attendance/clock-in", new ClockInput(site.Id, ExceptionReason: "Location access unavailable; working remotely by arrangement."));
        Assert.Equal("Unverified", row.Evidence);
        Assert.Contains("remotely", row.ExceptionReason);
        Assert.Null(row.DistanceMeters);
        row = await Post<SessionDto>(employee, "/api/attendance/clock-out", new { });
        Assert.Equal("Pending review", row.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/attendance/" + row.Id + "/review", new DecisionInput("Approved", "Own approval", row.ConcurrencyStamp))).StatusCode);
        row = await Post<SessionDto>(hr, "/api/attendance/" + row.Id + "/review", new DecisionInput("Approved", "Confirmed remote assignment with supervisor", row.ConcurrencyStamp));
        Assert.Equal("Approved", row.Status);
        Assert.Equal("Unverified", row.Evidence);
    }

    [Fact]
    public async Task ReviewedLegacyImportIsTenantScopedNonOverwritingAndRequiresPasswordChange()
    {
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MataHrDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Employees(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,Number nvarchar(64),Name nvarchar(160),Email nvarchar(254),Department nvarchar(160),Position nvarchar(160),ContractType nvarchar(64),Started date NOT NULL,Exited date NULL,ManagerId uniqueidentifier NULL); CREATE TABLE Accounts(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,Email nvarchar(254),Roles nvarchar(100),EmployeeId uniqueidentifier NULL,Active bit NOT NULL);");
        var legacyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync("INSERT Employees VALUES({0},{1},'LEGACY-01','Legacy Person','legacy@example.invalid','Engineering','Analyst','Permanent','2020-01-01',NULL,{2}); INSERT Employees VALUES({3},{4},'FOREIGN-01','Other Tenant','other@example.invalid','','','Permanent','2020-01-01',NULL,NULL); INSERT Accounts VALUES({5},{1},'legacy@example.invalid','Employee',{0},1);", legacyId, f.TenantA, f.Manager.Id, foreignId, f.TenantB, accountId);
        using var hr = await f.Client("hr");
        using var admin = await f.Client("admin");
        using var manager = await f.Client("manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/imports/preview", new LegacyPreviewInput("Employees"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsJsonAsync("/api/imports/preview", new LegacyPreviewInput("Accounts"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/imports/preview", new LegacyPreviewInput("Employees"))).StatusCode);
        var before = await Post<LegacyPreviewDto>(admin, "/api/imports/preview", new LegacyPreviewInput("Accounts"));
        Assert.False(before.CanCommit);
        var preview = await Post<LegacyPreviewDto>(hr, "/api/imports/preview", new LegacyPreviewInput("Employees"));
        Assert.Single(preview.Rows);
        Assert.Equal(legacyId, preview.Rows[0].SourceId);
        Assert.True(preview.CanCommit);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync("/api/imports/" + preview.Id + "/commit", new LegacyCommitInput(preview.Fingerprint, false))).StatusCode);
        var imported = await Post<LegacyCommitDto>(hr, "/api/imports/" + preview.Id + "/commit", new LegacyCommitInput(preview.Fingerprint, true));
        Assert.Equal(1, imported.Imported);
        var person = await hr.GetFromJsonAsync<EmployeeDto>("/api/employees/" + legacyId);
        Assert.Equal(f.Manager.Id, person!.ManagerId);
        Assert.NotNull(person.PositionId);
        var repeat = await Post<LegacyPreviewDto>(hr, "/api/imports/preview", new LegacyPreviewInput("Employees"));
        Assert.Equal("Already imported", Assert.Single(repeat.Rows).Status);
        var accounts = await Post<LegacyPreviewDto>(admin, "/api/imports/preview", new LegacyPreviewInput("Accounts"));
        Assert.True(accounts.CanCommit);
        var created = await Post<LegacyCommitDto>(admin, "/api/imports/" + accounts.Id + "/commit", new LegacyCommitInput(accounts.Fingerprint, true));
        var credential = Assert.Single(created.Credentials);
        using var login = f.NewClient();
        var authenticated = await login.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, credential.Email, credential.TemporaryPassword));
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await login.GetAsync("/api/employees")).StatusCode);
        var me = await login.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("requiresPasswordChange").GetBoolean());
        (await login.PostAsJsonAsync("/api/auth/password", new PasswordChangeInput(credential.TemporaryPassword, "Changed-Legacy-Password42!"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await login.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/imports/" + accounts.Id + "/commit", new LegacyCommitInput(accounts.Fingerprint, true))).StatusCode);
        var mapping = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LegacyImportOptions>>().Value;
        mapping.TargetTenantId = f.TenantA;
        mapping.SourceTenantId = f.TenantB;
        var mapped = await Post<LegacyPreviewDto>(hr, "/api/imports/preview", new LegacyPreviewInput("Employees"));
        Assert.Equal(f.TenantB, mapped.SourceTenantId);
        Assert.Equal(f.TenantA, mapped.TargetTenantId);
        Assert.Equal(foreignId, Assert.Single(mapped.Rows).SourceId);
        mapping.TargetTenantId = Guid.NewGuid();
        var restricted = await Post<LegacyPreviewDto>(hr, "/api/imports/preview", new LegacyPreviewInput("Employees"));
        Assert.Equal(f.TenantA, restricted.SourceTenantId);
        Assert.Equal(legacyId, Assert.Single(restricted.Rows).SourceId);
        mapping.TargetTenantId = null;
        mapping.SourceTenantId = null;
        var stale = await Post<LegacyPreviewDto>(admin, "/api/imports/preview", new LegacyPreviewInput("Accounts"));
        await db.Database.ExecuteSqlRawAsync("UPDATE Accounts SET Roles='Admin' WHERE Id={0}", accountId);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/imports/" + stale.Id + "/commit", new LegacyCommitInput(stale.Fingerprint, true))).StatusCode);
        var unchanged = await Post<LegacyPreviewDto>(admin, "/api/imports/preview", new LegacyPreviewInput("Accounts"));
        Assert.Equal("Already exists", Assert.Single(unchanged.Rows).Status);
        var skipped = await Post<LegacyCommitDto>(admin, "/api/imports/" + unchanged.Id + "/commit", new LegacyCommitInput(unchanged.Fingerprint, true));
        Assert.Equal(0, skipped.Imported);
        Assert.Empty(skipped.Credentials);
        var nativeAccounts = await admin.GetFromJsonAsync<Page<AccountDto>>("/api/admin/accounts?maxResultCount=200");
        Assert.Equal(new[] { "Employee" }, nativeAccounts!.Items.Single(x => x.Email == credential.Email).Roles);
        // The source remains intact and can be reconciled again without replacing native credentials.
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Employees").SingleAsync());
    }

    [Fact]
    public async Task OrganisationDirectoryShowsStructureWithoutConfidentialEmployeeFields()
    {
        using var employee = await f.Client("alice");
        using var manager = await f.Client("manager");
        var structure = await employee.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/organisation/structure");
        var people = structure.GetProperty("employees");
        Assert.Contains(people.EnumerateArray(), x => x.GetProperty("id").GetGuid() == f.Outsider.Id);
        Assert.DoesNotContain(people.EnumerateArray(), x => x.GetProperty("id").GetGuid() == f.Bob.Id);
        foreach (var person in people.EnumerateArray())
        {
            Assert.False(person.TryGetProperty("email", out _));
            Assert.False(person.TryGetProperty("started", out _));
            Assert.False(person.TryGetProperty("contractType", out _));
        }

        var direct = await manager.GetFromJsonAsync<StructureDto>("/api/organisation/structure");
        Assert.DoesNotContain(direct!.Employees, x => x.Id == f.Outsider.Id);
        var own = await employee.GetFromJsonAsync<Page<EmployeeDto>>("/api/employees");
        Assert.Equal(f.Alice.Id, Assert.Single(own!.Items).Id);
    }

    [Fact]
    public async Task RoleGrantDirectoryAndChartCanvasMatchTheirPermissions()
    {
        using var admin=await f.Client("admin");using var hr=await f.Client("hr");using var employee=await f.Client("alice");
        var hrGrants=await admin.GetFromJsonAsync<List<RoleGrantDto>>("/api/admin/role-permissions?role=HR");Assert.True(hrGrants!.Single(x=>x.Permission==WorkforcePermissions.PayrollManage).Granted);
        var ownGrants=await admin.GetFromJsonAsync<List<RoleGrantDto>>("/api/admin/role-permissions?role=Employee");Assert.False(ownGrants!.Single(x=>x.Permission==WorkforcePermissions.PayrollManage).Granted);
        Assert.Equal(HttpStatusCode.Forbidden,(await employee.GetAsync("/api/admin/employee-links")).StatusCode);
        var links=await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin/employee-links");foreach(var link in links.EnumerateArray()){Assert.True(link.TryGetProperty("number",out _));Assert.False(link.TryGetProperty("email",out _));Assert.False(link.TryGetProperty("contractType",out _));}
        (await hr.PutAsJsonAsync("/api/organisation/canvas",new ChartPointDto(480,96))).EnsureSuccessStatusCode();
        var structure=await hr.GetFromJsonAsync<StructureDto>("/api/organisation/structure");Assert.Equal(new ChartPointDto(480,96),structure!.Company);
        Assert.Equal(HttpStatusCode.Forbidden,(await employee.PutAsJsonAsync("/api/organisation/canvas",new ChartPointDto(0,0))).StatusCode);
    }

    [Theory]
    [InlineData(-29.78, 30.77, 10, true)]
    [InlineData(-28, 30.77, 10, false)]
    [InlineData(-29.78, 30.77, 100, false)]
    public void GeofenceAccountsForAccuracyAndRejectsBadLocation(double latitude, double longitude, double accuracy, bool valid)
    {
        var location = new ClockLocation();
        location.ConfigureBoundary(-29.78, 30.77, 150);
        if (valid)
            Assert.Equal(0, PresenceRules.Validate(location, latitude, longitude, accuracy, f.Now, f.Now));
        else
            Assert.Throws<WorkforceRuleException>(() => PresenceRules.Validate(location, latitude, longitude, accuracy, f.Now, f.Now));
    }
}
