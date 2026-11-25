using System.Net;
using System.Net.Http.Json;
using MataHR.Workforce.Contracts;
using Xunit;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace MataHR.Workforce.Tests;
public class BoundaryTests(WorkforceFixture f) : IClassFixture<WorkforceFixture>
{
    [Fact]
    public async Task OpenApiDocumentCanBeGeneratedWithDateOnlyContracts()
    {
        using var client = f.Factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync() + "\n" + f.Logs.Text);
        using var schema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(schema.RootElement.GetProperty("paths").TryGetProperty("/api/employees", out _));
        var explorer = await client.GetAsync("/swagger/index.html");
        explorer.EnsureSuccessStatusCode();
        Assert.Contains("MataHR API Explorer", await explorer.Content.ReadAsStringAsync());
        var initializer = await client.GetStringAsync("/swagger/index.js");
        Assert.Contains("/openapi/v1.json", initializer);
        Assert.Contains("X-MataHR-Request", initializer);
        Assert.Contains("same-origin", initializer);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/swagger-ui-bundle.js")).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndMissingCsrfAreRejected()
    {
        using var c = f.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/login", new { })).StatusCode);
    }

    [Fact]
    public async Task LoginPrivacyAndLogoutAreDatabaseBacked()
    {
        using var c = await f.Client("privacy", false);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/privacy", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RolesAndTenantBoundariesAreEnforced()
    {
        using var employee = await f.Client("alice");
        var own = (await employee.GetFromJsonAsync<Page<EmployeeDto>>("/api/employees"))!;
        Assert.Single(own.Items);
        Assert.Equal(f.Alice.Id, own.Items[0].Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/payroll/salaries")).StatusCode);
        using var manager = await f.Client("manager");
        var team = (await manager.GetFromJsonAsync<Page<EmployeeDto>>("/api/employees"))!;
        Assert.Contains(team.Items, x => x.Id == f.Alice.Id);
        Assert.DoesNotContain(team.Items, x => x.Id == f.Outsider.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/employees/" + f.Outsider.Id)).StatusCode);
        using var hr = await f.Client("hr");
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync("/api/employees/" + f.Bob.Id)).StatusCode);
        using var foreign = await f.Client("foreign");
        Assert.Empty((await foreign.GetFromJsonAsync<List<CompensationDto>>("/api/payroll/salaries"))!);
        Assert.Empty((await foreign.GetFromJsonAsync<List<PolicyDto>>("/api/leave/policies"))!);
        employee.DefaultRequestHeaders.Add("__tenant", f.TenantB.ToString());
        Assert.Equal(f.Alice.Id, (await employee.GetFromJsonAsync<Page<EmployeeDto>>("/api/employees"))!.Items.Single().Id);
        using var admin = await f.Client("admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/payroll/salaries")).StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<Page<EmployeeDto>>("/api/employees"))!.Items);
    }

    [Fact]
    public async Task StaleUpdatesAndReportingCyclesAreRejected()
    {
        using var hr = await f.Client("hr");
        var alice = (await hr.GetFromJsonAsync<EmployeeDto>("/api/employees/" + f.Alice.Id))!;
        var input = new EmployeeInput(alice.Number, alice.Name, "updated@example.invalid", alice.Started, alice.Exited, alice.ContractType, alice.ManagerId, alice.PositionId, alice.ConcurrencyStamp);
        Assert.Equal(HttpStatusCode.OK, (await hr.PutAsJsonAsync("/api/employees/" + alice.Id, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await hr.PutAsJsonAsync("/api/employees/" + alice.Id, input)).StatusCode);
        var manager = (await hr.GetFromJsonAsync<EmployeeDto>("/api/employees/" + f.Manager.Id))!;
        var cycle = await hr.PutAsJsonAsync("/api/organisation/assignments", new AssignmentInput(manager.Id, null, alice.Id, manager.ConcurrencyStamp));
        Assert.False(cycle.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DocumentsArePrivateAndContractsRequireIndependentEvidence()
    {
        using var hr = await f.Client("hr");
        using var hr2 = await f.Client("hr2");
        using var employee = await f.Client("outsider");
        var content = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF"));
        var upload = await hr.PostAsJsonAsync("/api/documents", new DocumentInput(f.Alice.Id, "appointment.pdf", "application/pdf", content, "Contract"));
        Assert.True(upload.IsSuccessStatusCode, (await upload.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        var doc = (await upload.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/documents/" + doc.Id + "/download")).StatusCode);
        Assert.False((await hr.PostAsJsonAsync("/api/documents/" + doc.Id + "/verify", new DocumentVerificationInput("UnsignedContract", "Independent unsigned document check"))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hr2.PostAsJsonAsync("/api/documents/" + doc.Id + "/verify", new DocumentVerificationInput("UnsignedContract", "Independent unsigned document check"))).StatusCode);
        var created = await hr.PostAsJsonAsync("/api/contracts", new ContractInput(f.Alice.Id, "TEST-" + Guid.NewGuid().ToString("N"), new DateOnly(2025, 1, 1), null));
        Assert.True(created.IsSuccessStatusCode, (await created.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        var contract = (await created.Content.ReadFromJsonAsync<ContractDto>())!;
        var submitted = await hr.PostAsJsonAsync("/api/contracts/" + contract.Id + "/transition", new ContractDecision("InReview", "Ready", doc.Id, contract.ConcurrencyStamp));
        Assert.True(submitted.IsSuccessStatusCode, (await submitted.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        contract = (await submitted.Content.ReadFromJsonAsync<ContractDto>())!;
        Assert.False((await hr.PostAsJsonAsync("/api/contracts/" + contract.Id + "/transition", new ContractDecision("Approved", "Review complete", null, contract.ConcurrencyStamp))).IsSuccessStatusCode);
        var approved = await hr2.PostAsJsonAsync("/api/contracts/" + contract.Id + "/transition", new ContractDecision("Approved", "Review complete", null, contract.ConcurrencyStamp));
        Assert.True(approved.IsSuccessStatusCode, (await approved.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
    }

    [Fact]
    public async Task RegisterRejectsDuplicateClockingAndSelfApproval()
    {
        using var employee = await f.Client("outsider");
        using var hr = await f.Client("hr");
        var response = await employee.PostAsJsonAsync("/api/attendance/clock-in", new ClockInput(f.Location.Id));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal("Unverified", session.Evidence);
        Assert.False((await employee.PostAsJsonAsync("/api/attendance/clock-in", new ClockInput(f.Location.Id))).IsSuccessStatusCode);
        response = await employee.PostAsJsonAsync("/api/attendance/clock-out", new { });
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/attendance/" + session.Id + "/review", new DecisionInput("Approved", "Own", session.ConcurrencyStamp))).StatusCode);
        response = await hr.PostAsJsonAsync("/api/attendance/" + session.Id + "/review", new DecisionInput("Approved", "Checked register", session.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
    }

    [Fact]
    public async Task LeaveRequiresManagerThenDifferentHr()
    {
        using var employee = await f.Client("alice");
        using var manager = await f.Client("manager");
        using var hr = await f.Client("hr");
        var day = DateOnly.FromDateTime(f.Now.UtcDateTime.AddHours(2)).AddDays(10);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        var response = await employee.PostAsJsonAsync("/api/leave/requests", new LeaveInput(f.Policy.Id, day, day, "Annual leave"));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        var request = (await response.Content.ReadFromJsonAsync<LeaveDto>())!;
        Assert.False((await employee.PostAsJsonAsync("/api/leave/requests", new LeaveInput(f.Policy.Id, day, day, "Duplicate"))).IsSuccessStatusCode);
        response = await manager.PostAsJsonAsync("/api/leave/requests/" + request.Id + "/review", new DecisionInput("Approved", "Team covered", request.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        request = (await response.Content.ReadFromJsonAsync<LeaveDto>())!;
        Assert.Equal("HR", request.Stage);
        Assert.Equal("Pending", request.Status);
        response = await hr.PostAsJsonAsync("/api/leave/requests/" + request.Id + "/review", new DecisionInput("Approved", "Checked entitlement", request.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        request = (await response.Content.ReadFromJsonAsync<LeaveDto>())!;
        Assert.Equal("Approved", request.Status);
    }

    [Fact]
    public async Task PayrollRequiresIndependentApprovalAndKeepsSnapshot()
    {
        using var hr = await f.Client("hr");
        using var hr2 = await f.Client("hr2");
        var period = DateOnly.FromDateTime(f.Now.UtcDateTime).AddMonths(-2).ToString("yyyy-MM");
        var readiness = (await hr.GetFromJsonAsync<PayrollReadiness>("/api/payroll/readiness?period=" + period))!;
        Assert.True(readiness.Ready, string.Join(";", readiness.Issues));
        var response = await hr.PostAsJsonAsync("/api/payroll/runs", new RunInput(period));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        var run = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        response = await hr.PostAsJsonAsync("/api/payroll/runs/" + run.Id + "/transition", new DecisionInput("Reviewed", "Inputs reconciled", run.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        run = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        Assert.False((await hr.PostAsJsonAsync("/api/payroll/runs/" + run.Id + "/transition", new DecisionInput("Approved", "Own approval", run.ConcurrencyStamp))).IsSuccessStatusCode);
        response = await hr2.PostAsJsonAsync("/api/payroll/runs/" + run.Id + "/transition", new DecisionInput("Approved", "Independent check", run.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + "\n" + f.Logs.Text);
        run = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        var export = (await hr.GetFromJsonAsync<ExportDto>("/api/payroll/runs/" + run.Id + "/export"))!;
        Assert.Equal(run.Hash, export.Hash);
        Assert.Contains(f.Alice.Id.ToString(), export.ContentJson);
    }

    [Fact]
    public async Task ApplyingMigrationsPreservesExistingTables()
    {
        await using var c = new Microsoft.Data.SqlClient.SqlConnection(f.Connection);
        await c.OpenAsync();
        await using var q = c.CreateCommand();
        q.CommandText = "SELECT Value FROM LegacySentinel WHERE Id=1";
        Assert.Equal("Preserved record", await q.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ConcurrentClockInAllowsOnlyOneOpenSession()
    {
        using var a = await f.Client("alice");
        using var b = await f.Client("alice");
        var responses = await Task.WhenAll(a.PostAsJsonAsync("/api/attendance/clock-in", new ClockInput(f.Location.Id)), b.PostAsJsonAsync("/api/attendance/clock-in", new ClockInput(f.Location.Id)));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.DoesNotContain(responses, x => (int)x.StatusCode >= 500);
        Assert.True((await a.PostAsJsonAsync("/api/attendance/clock-out", new { })).IsSuccessStatusCode);
    }

    [Fact]
    public async Task ConcurrentOverlappingLeaveIsNotDoubleReserved()
    {
        using var a = await f.Client("outsider");
        using var b = await f.Client("outsider");
        var day = DateOnly.FromDateTime(f.Now.UtcDateTime.AddHours(2)).AddDays(18);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        var input = new LeaveInput(f.Policy.Id, day, day, "Concurrent reservation");
        var responses = await Task.WhenAll(a.PostAsJsonAsync("/api/leave/requests", input), b.PostAsJsonAsync("/api/leave/requests", input));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.DoesNotContain(responses, x => (int)x.StatusCode >= 500);
    }

    [Fact]
    public async Task PayslipsRemainPrivateUntilExplicitRelease()
    {
        using var hr = await f.Client("hr");
        using var hr2 = await f.Client("hr2");
        using var employee = await f.Client("alice");
        var period = DateOnly.FromDateTime(f.Now.UtcDateTime).AddMonths(-4).ToString("yyyy-MM");
        var response = await hr.PostAsJsonAsync("/api/payroll/runs", new RunInput(period));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var run = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        response = await hr.PostAsJsonAsync("/api/payroll/runs/" + run.Id + "/transition", new DecisionInput("Reviewed", "Checked", run.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        run = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        response = await hr2.PostAsJsonAsync("/api/payroll/runs/" + run.Id + "/transition", new DecisionInput("Approved", "Independent check", run.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var pdf = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF"));
        response = await hr.PostAsJsonAsync("/api/documents", new DocumentInput(f.Alice.Id, "payslip.pdf", "application/pdf", pdf, "Payslip"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var doc = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.True((await hr2.PostAsJsonAsync("/api/documents/" + doc.Id + "/verify", new DocumentVerificationInput("Payslip", "Verified payroll-provider payslip"))).IsSuccessStatusCode);
        response = await hr.PostAsJsonAsync("/api/payroll/runs/" + run.Id + "/payslips", new PayslipInput(f.Alice.Id, doc.Id));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var slip = (await response.Content.ReadFromJsonAsync<PayslipDto>())!;
        Assert.DoesNotContain((await employee.GetFromJsonAsync<List<PayslipDto>>("/api/payroll/payslips"))!, x => x.Id == slip.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/documents/" + doc.Id + "/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/payroll/payslips/" + slip.Id + "/download")).StatusCode);
        Assert.True((await hr.PostAsJsonAsync("/api/payroll/payslips/" + slip.Id + "/release", new { })).IsSuccessStatusCode);
        var download = (await employee.GetFromJsonAsync<DocumentContent>("/api/payroll/payslips/" + slip.Id + "/download"))!;
        Assert.Equal(pdf, download.ContentBase64);
    }

    [Fact]
    public async Task CorrectionAndOvertimeRequireIndependentReview()
    {
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MataHR.Persistence.WorkforceDbContext>();
        var yesterday = f.Now.UtcDateTime.Date.AddDays(-1);
        var session = new WorkSession(f.TenantA, f.Alice.Id, f.Location.Id, new DateTimeOffset(yesterday.AddHours(9), TimeSpan.Zero));
        session.ClockOut(new DateTimeOffset(yesterday.AddHours(17), TimeSpan.Zero));
        session.Review(true, Guid.NewGuid(), null, "Seed reviewed attendance");
        db.Add(session);
        await db.SaveChangesAsync();
        using var employee = await f.Client("alice");
        using var manager = await f.Client("manager");
        var request = new TimeRequestInput("Correction", session.Id, session.Date, session.Start.AddHours(-1), session.End!.Value, "Missing morning time");
        var response = await employee.PostAsJsonAsync("/api/attendance/requests", request);
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var correction = (await response.Content.ReadFromJsonAsync<TimeRequestDto>())!;
        response = await manager.PostAsJsonAsync("/api/attendance/requests/" + correction.Id + "/review", new DecisionInput("Approved", "Confirmed", correction.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        request = new TimeRequestInput("Overtime", session.Id, session.Date, session.End!.Value.AddHours(-1), session.End!.Value, "Approved project work");
        response = await employee.PostAsJsonAsync("/api/attendance/requests", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var overtime = (await response.Content.ReadFromJsonAsync<TimeRequestDto>())!;
        response = await manager.PostAsJsonAsync("/api/attendance/requests/" + overtime.Id + "/review", new DecisionInput("Approved", "Confirmed extra hour", overtime.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
    }

    [Fact]
    public async Task AdminCanAssignRolesAndLinkEmployeesWithoutPayrollAccess()
    {
        using var admin = await f.Client("admin");
        var email = "provisioned-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        var response = await admin.PostAsJsonAsync("/api/admin/accounts", new AccountInput(email, WorkforceFixture.Password, ["HR"], null));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var account = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        response = await admin.PutAsJsonAsync("/api/admin/accounts/" + account.Id, new AccountUpdateInput(["HR"], f.Alice.Id, true, account.ConcurrencyStamp));
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var updated = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal(f.Alice.Id, updated.EmployeeId);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/payroll/salaries")).StatusCode);
    }
}
