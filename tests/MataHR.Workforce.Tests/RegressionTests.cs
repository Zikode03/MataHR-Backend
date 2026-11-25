using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MataHR.Persistence;
using MataHR.Workforce.Contracts;
using Xunit;

namespace MataHR.Workforce.Tests;
public class RegressionTests(WorkforceFixture f) : IClassFixture<WorkforceFixture>
{
    async Task<T> Post<T>(HttpClient c, string url, object input)
    {
        var response = await c.PostAsJsonAsync(url, input);
        Assert.True(response.IsSuccessStatusCode, (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task HistoricalPayrollUsesTheEffectiveRevision()
    {
        using var hr = await f.Client("hr");
        var result = await hr.PutAsJsonAsync("/api/payroll/salaries", new CompensationInput(f.Alice.Id, 18000, 0, 0, 110, 1.5m, new DateOnly(2026, 6, 1)));
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        result = await hr.PutAsJsonAsync("/api/payroll/salaries", new CompensationInput(f.Alice.Id, 22000, 0, 0, 120, 1.5m, new DateOnly(2026, 9, 1)));
        Assert.True(result.IsSuccessStatusCode);
        var may = (await hr.GetFromJsonAsync<PayrollReadiness>("/api/payroll/readiness?period=2026-05"))!;
        var june = (await hr.GetFromJsonAsync<PayrollReadiness>("/api/payroll/readiness?period=2026-06"))!;
        Assert.Equal(15000, may.Rows.Single(x => x.EmployeeId == f.Alice.Id).Basic);
        Assert.Equal(18000, june.Rows.Single(x => x.EmployeeId == f.Alice.Id).Basic);
        Assert.False((await hr.PutAsJsonAsync("/api/payroll/salaries", new CompensationInput(f.Alice.Id, 999, 0, 0, 0, 1.5m, new DateOnly(2026, 6, 1)))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task BenefitsHaveHistoricalEffectiveDates()
    {
        using var hr = await f.Client("hr");
        var result = await hr.PostAsJsonAsync("/api/payroll/benefits", new BenefitInput(f.Alice.Id, "Medical", 500, 200, new DateOnly(2026, 7, 1)));
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        var june = (await hr.GetFromJsonAsync<PayrollReadiness>("/api/payroll/readiness?period=2026-06"))!;
        var july = (await hr.GetFromJsonAsync<PayrollReadiness>("/api/payroll/readiness?period=2026-07"))!;
        Assert.Equal(0, june.Rows.Single(x => x.EmployeeId == f.Alice.Id).EmployerBenefits);
        Assert.Equal(500, july.Rows.Single(x => x.EmployeeId == f.Alice.Id).EmployerBenefits);
    }

    [Fact]
    public async Task RequiredHrStageAppliesToCancellation()
    {
        using var employee = await f.Client("alice");
        using var manager = await f.Client("manager");
        using var hr = await f.Client("hr");
        var request = await Post<LeaveDto>(employee, "/api/leave/requests", new LeaveInput(f.Policy.Id, new DateOnly(2026, 7, 7), new DateOnly(2026, 7, 7), "Annual leave"));
        request = await Post<LeaveDto>(manager, $"/api/leave/requests/{request.Id}/review", new DecisionInput("Approved", "Team covered", request.ConcurrencyStamp));
        request = await Post<LeaveDto>(hr, $"/api/leave/requests/{request.Id}/review", new DecisionInput("Approved", "Balance verified", request.ConcurrencyStamp));
        request = await Post<LeaveDto>(employee, $"/api/leave/requests/{request.Id}/cancel", request.ConcurrencyStamp);
        request = await Post<LeaveDto>(manager, $"/api/leave/requests/{request.Id}/review", new DecisionInput("Approved", "Cancellation agreed", request.ConcurrencyStamp));
        Assert.Equal("Cancellation pending", request.Status);
        Assert.Equal("HR", request.Stage);
        request = await Post<LeaveDto>(hr, $"/api/leave/requests/{request.Id}/review", new DecisionInput("Approved", "Cancellation checked", request.ConcurrencyStamp));
        Assert.Equal("Cancelled", request.Status);
    }

    [Fact]
    public async Task UnsignedOrIdenticalContentCannotBecomeSignedEvidence()
    {
        using var hr = await f.Client("hr");
        using var hr2 = await f.Client("hr2");
        var original = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.7\nUnsigned appointment\n%%EOF"));
        var doc = await Post<DocumentDto>(hr, "/api/documents", new DocumentInput(f.Alice.Id, "unsigned.pdf", "application/pdf", original, "Contract"));
        await Post<DocumentDto>(hr2, $"/api/documents/{doc.Id}/verify", new DocumentVerificationInput("UnsignedContract", "Unsigned version checked"));
        var contract = await Post<ContractDto>(hr, "/api/contracts", new ContractInput(f.Alice.Id, "REG-" + Guid.NewGuid(), new DateOnly(2025, 1, 1), null));
        contract = await Post<ContractDto>(hr, $"/api/contracts/{contract.Id}/transition", new ContractDecision("InReview", "Submitted", doc.Id, contract.ConcurrencyStamp));
        contract = await Post<ContractDto>(hr2, $"/api/contracts/{contract.Id}/transition", new ContractDecision("Approved", "Reviewed", null, contract.ConcurrencyStamp));
        contract = await Post<ContractDto>(hr, $"/api/contracts/{contract.Id}/transition", new ContractDecision("AwaitingSignature", "Sent to employee", null, contract.ConcurrencyStamp));
        Assert.False((await hr.PostAsJsonAsync($"/api/contracts/{contract.Id}/transition", new ContractDecision("Signed", "Wrong evidence", doc.Id, contract.ConcurrencyStamp))).IsSuccessStatusCode);
        var same = await Post<DocumentDto>(hr, "/api/documents", new DocumentInput(f.Alice.Id, "same.pdf", "application/pdf", original, "Contract"));
        await Post<DocumentDto>(hr2, $"/api/documents/{same.Id}/verify", new DocumentVerificationInput("SignedContract", "Attestation cannot override equal content", new DateOnly(2026, 6, 14)));
        Assert.False((await hr.PostAsJsonAsync($"/api/contracts/{contract.Id}/transition", new ContractDecision("Signed", "Same content", same.Id, contract.ConcurrencyStamp))).IsSuccessStatusCode);
        var signed = await Post<DocumentDto>(hr, "/api/documents", new DocumentInput(f.Alice.Id, "signed.pdf", "application/pdf", Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.7\nSigned paper evidence\n%%EOF")), "Contract"));
        await Post<DocumentDto>(hr2, $"/api/documents/{signed.Id}/verify", new DocumentVerificationInput("SignedContract", "Both signatures inspected on signed copy", new DateOnly(2026, 6, 14)));
        contract = await Post<ContractDto>(hr, $"/api/contracts/{contract.Id}/transition", new ContractDecision("Signed", "Verified signed evidence", signed.Id, contract.ConcurrencyStamp));
        Assert.Equal("Signed", contract.Status);
    }

    [Fact]
    public async Task ChangingSourcesWithoutChangingHoursInvalidatesPayrollReview()
    {
        using var hr = await f.Client("hr");
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
        var session = new WorkSession(f.TenantA, f.Alice.Id, f.Location.Id, new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero));
        session.ClockOut(session.Start.AddHours(8));
        session.Review(true, Guid.NewGuid(), null, "Test seed");
        db.Add(session);
        await db.SaveChangesAsync();
        var run = await Post<RunDto>(hr, "/api/payroll/runs", new RunInput("2026-03"));
        session.Correct(session.Start.AddMinutes(30), session.End!.Value.AddMinutes(30), Guid.NewGuid(), null, "Corrected start and finish with same total hours");
        await db.SaveChangesAsync();
        Assert.False((await hr.PostAsJsonAsync($"/api/payroll/runs/{run.Id}/transition", new DecisionInput("Reviewed", "Must regenerate", run.ConcurrencyStamp))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task PersistedPermissionRevocationIsEnforced()
    {
        using var admin = await f.Client("admin");
        using var hr = await f.Client("hr");
        try
        {
            Assert.True((await admin.PutAsJsonAsync("/api/admin/role-permissions", new RolePermissionInput("HR", WorkforcePermissions.PayrollManage, false))).IsSuccessStatusCode);
            await using (var sql = new Microsoft.Data.SqlClient.SqlConnection(f.Connection))
            {
                await sql.OpenAsync();
                await using var q = sql.CreateCommand();
                q.CommandText = "SELECT COUNT(*) FROM AbpPermissionGrants WHERE Name=@name AND ProviderName='R' AND ProviderKey='HR' AND TenantId=@tenant";
                q.Parameters.AddWithValue("@name", WorkforcePermissions.PayrollManage);
                q.Parameters.AddWithValue("@tenant", f.TenantA);
                Assert.Equal(0, Convert.ToInt32(await q.ExecuteScalarAsync()));
            }

            Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync("/api/payroll/salaries")).StatusCode);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/admin/role-permissions", new RolePermissionInput("HR", WorkforcePermissions.PayrollManage, true));
        }

        Assert.Equal(HttpStatusCode.OK, (await hr.GetAsync("/api/payroll/salaries")).StatusCode);
    }
}
