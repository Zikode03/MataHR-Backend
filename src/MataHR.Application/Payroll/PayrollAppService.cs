using MataHR.Attendance;
using MataHR.Auditing;
using MataHR.Documents;
using MataHR.Employees;
using MataHR.Leave;
using MataHR.Common;
using MataHR.Common.Contracts;
using MataHR.Documents.Contracts;
using MataHR.Payroll.Contracts;
using MataHR.Permissions;
using MataHR.Scheduling;
using Volo.Abp.Authorization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MataHR.Payroll;
public class PayrollAppService(IRepository<Employee, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<Compensation, Guid> salaries, IRepository<EmployeeBenefit, Guid> benefits, IRepository<WorkSession, Guid> sessions, IRepository<TimeRequest, Guid> timeRequests, IRepository<LeaveRequest, Guid> leave, IRepository<PayRun, Guid> runs, IRepository<ReleasedPayslip, Guid> payslips, IRepository<SecureDocument, Guid> documents, CalendarService calendar, PayrollCalculator calculator) : WorkforceService(workers, audits), IPayrollAppService
{
    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<DocumentContent> DownloadProviderDocumentAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(WorkforcePermissions.PayrollManage);
        var doc=await Get(documents,id);
        Require(doc.Kind=="Payslip","A payroll-provider payslip is required.");
        await Audit("Payslip.EvidenceDownloaded",id);
        return new(doc.Name,doc.ContentType,Convert.ToBase64String(doc.Content),doc.Sha256);
    }

    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<List<DocumentDto>> GetProviderDocumentsAsync() => await Queries.ToListAsync((await documents.GetQueryableAsync()).Where(x => x.Kind == "Payslip").Select(x => new DocumentDto(x.Id, x.EmployeeId, x.Name, x.Kind, x.ContentType, x.Content.Length, x.Sha256, x.Expires, x.VerifiedBy.HasValue)));
    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<List<CompensationDto>> GetSalariesAsync() => (await salaries.GetListAsync()).Select(x => new CompensationDto(x.EmployeeId, x.Basic, x.Allowance, x.Deduction, x.HourlyRate, x.OvertimeMultiplier, x.EffectiveFrom)).ToList();
    [Authorize(WorkforcePermissions.PayrollManage), SerializableCommand]
    public virtual async Task SetSalaryAsync(CompensationInput input)
    {
        await InSerializableTransaction(async () =>
        {
            await GetEmployee(input.EmployeeId);
            await calendar.EffectiveChangeAllowed(input.EffectiveFrom);
            Require(!await salaries.AnyAsync(x => x.EmployeeId == input.EmployeeId && x.EffectiveFrom == input.EffectiveFrom), "A compensation revision already exists for that effective date; history cannot be overwritten.");
            var row = await salaries.InsertAsync(new Compensation(TenantId, input.EmployeeId, input.EffectiveFrom, input.Basic, input.Allowance, input.Deduction, input.HourlyRate, input.OvertimeMultiplier));
            await Audit("Compensation.RevisionCreated", row.Id);
        });
    }

    [Authorize(WorkforcePermissions.PayrollOwn)]
    public virtual async Task<List<BenefitDto>> GetBenefitsAsync()
    {
        var q = await benefits.GetQueryableAsync();
        return (await Queries.ToListAsync(q.Where(x => Hr || (x.EmployeeId == Own && x.EffectiveFrom <= Today && (x.EffectiveTo == null || x.EffectiveTo > Today))))).Select(x => new BenefitDto(x.Id, x.EmployeeId, x.Name, x.Employer, x.EmployeeCost, x.Active, x.EffectiveFrom, x.EffectiveTo)).ToList();
    }

    [Authorize(WorkforcePermissions.PayrollManage), SerializableCommand]
    public virtual async Task<BenefitDto> CreateBenefitAsync(BenefitInput input)
    {
        return await InSerializableTransaction<BenefitDto>(async () =>
        {
            await GetEmployee(input.EmployeeId);
            await calendar.EffectiveChangeAllowed(input.EffectiveFrom);
            Require(!await benefits.AnyAsync(x => x.EmployeeId == input.EmployeeId && x.Name == input.Name && (input.EffectiveTo == null || x.EffectiveFrom < input.EffectiveTo) && (x.EffectiveTo == null || x.EffectiveTo > input.EffectiveFrom)), "Overlapping enrollment for the same benefit.");
            var row = await benefits.InsertAsync(new EmployeeBenefit(TenantId, input.EmployeeId, input.Name, input.Employer, input.EmployeeCost, input.EffectiveFrom, input.EffectiveTo));
            await Audit("Benefit.Enrolled", row.Id);
            return new(row.Id, row.EmployeeId, row.Name, row.Employer, row.EmployeeCost, row.Active, row.EffectiveFrom, row.EffectiveTo);
        });
    }

    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<PayrollReadiness> GetReadinessAsync(string period) => await Readiness(period);
    async Task<PayrollReadiness> Readiness(string period)
    {
        DateOnly start, end;
        try
        {
            (start, end) = WorkforceRules.Period(period);
        }
        catch (ArgumentException e)
        {
            throw new Volo.Abp.UserFriendlyException(e.Message);
        }

        var people = await workers.GetListAsync(x => x.Started <= end && (x.Exited == null || x.Exited > start));
        var issues = new List<string>();
        var rows = new List<PayrollRow>();
        if (people.Count == 0)
            issues.Add("No active employees in this period.");
        var ids = people.Select(x => x.Id).ToArray();
        var allPay = await salaries.GetListAsync(x => ids.Contains(x.EmployeeId) && x.EffectiveFrom <= start);
        var allBenefits = await benefits.GetListAsync(x => ids.Contains(x.EmployeeId) && x.EffectiveFrom <= start && (x.EffectiveTo == null || x.EffectiveTo > start));
        var allSessions = await sessions.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Date >= start && x.Date <= end);
        var allTime = await timeRequests.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Date >= start && x.Date <= end);
        var allLeave = await leave.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Start <= end && x.End >= start);
        foreach (var employee in people.OrderBy(x => x.Id))
        {
            var pay = allPay.Where(x => x.EmployeeId == employee.Id).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
            if (pay == null)
            {
                issues.Add("Missing effective compensation: " + employee.Number);
                continue;
            }

            if (pay.EffectiveFrom == DateOnly.MinValue)
                issues.Add("Legacy compensation effective date requires reconciliation: " + employee.Number);
            var bs = allBenefits.Where(x => x.EmployeeId == employee.Id).OrderBy(x => x.Id).ToList();
            var ss = allSessions.Where(x => x.EmployeeId == employee.Id).OrderBy(x => x.Id).ToList();
            var ts = allTime.Where(x => x.EmployeeId == employee.Id).OrderBy(x => x.Id).ToList();
            var ls = allLeave.Where(x => x.EmployeeId == employee.Id).OrderBy(x => x.Id).ToList();
            if (bs.Any(x => x.EffectiveFrom == DateOnly.MinValue))
                issues.Add("Legacy benefit effective dates require reconciliation: " + employee.Number);
            var totals = calculator.Calculate(pay, bs, ss, ts, ls, start, end);
            if (totals.Pending > 0)
                issues.Add("Resolve pending attendance, overtime or leave: " + employee.Number);
            if (totals.OvertimeHours > 0 && pay.HourlyRate == 0)
                issues.Add("Overtime hourly rate missing: " + employee.Number);
            var source = JsonSerializer.Serialize(new { Pay = new { pay.Id, pay.ConcurrencyStamp }, Benefits = bs.Select(x => new { x.Id, x.ConcurrencyStamp }), Sessions = ss.Select(x => new { x.Id, x.ConcurrencyStamp }), TimeRequests = ts.Select(x => new { x.Id, x.ConcurrencyStamp }), Leave = ls.Select(x => new { x.Id, x.ConcurrencyStamp }) });
            rows.Add(new(employee.Id, employee.Number, pay.Basic, pay.Allowance, pay.Deduction, totals.EmployerBenefits, totals.EmployeeBenefits, totals.Hours, totals.OvertimeHours, totals.OvertimePay, totals.UnpaidDays, totals.Pending, Hash(source)));
        }

        return new(period, issues.Count == 0, issues, rows);
    }

    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<List<RunDto>> GetRunsAsync() => (await runs.GetListAsync()).OrderByDescending(x => x.Period).Select(Dto).ToList();
    [Authorize(WorkforcePermissions.PayrollManage), SerializableCommand]
    public virtual async Task<RunDto> PrepareAsync(RunInput input)
    {
        return await InSerializableTransaction<RunDto>(async () =>
        {
            var readiness = await Readiness(input.Period);
            Require(readiness.Rows.Count > 0, "No complete payroll inputs.");
            var row = await runs.FindAsync(x => x.Period == input.Period);
            if (row == null)
                row = await runs.InsertAsync(new PayRun(TenantId, input.Period));
            row.Prepare(JsonSerializer.Serialize(readiness.Rows), Actor);
            await runs.UpdateAsync(row);
            await Audit("Payroll.Prepared", row.Id);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.PayrollManage), SerializableCommand]
    public virtual async Task<RunDto> TransitionAsync(Guid id, DecisionInput input)
    {
        return await InSerializableTransaction<RunDto>(async () =>
        {
            var row = await Get(runs, id);
            Stamp(row, input.ConcurrencyStamp);
            Require(!string.IsNullOrWhiteSpace(input.Note) && (row.Status, input.Status)is ("Draft", "Reviewed") or ("Reviewed", "Approved") or ("Reviewed", "Draft"), "Invalid payroll transition or missing review note.");
            if (input.Status != "Draft")
            {
                // A reviewer must approve the exact inputs that were prepared, even when totals still match.
                var live = await Readiness(row.Period);
                Require(live.Ready && Hash(JsonSerializer.Serialize(live.Rows)) == row.Hash, "Payroll inputs changed or need attention. Regenerate the draft.");
            }

            Require(Enum.TryParse<PayRunState>(input.Status, out var next), "Invalid payroll state.");
            row.Transition(next, Actor, input.Note);
            await runs.UpdateAsync(row);
            await Audit("Payroll." + input.Status, id, input.Note);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<ExportDto> ExportAsync(Guid id)
    {
        var row = await Get(runs, id);
        Require(row.Status == "Approved", "Only approved payroll inputs can be exported.");
        await Audit("Payroll.Exported", id);
        return new(row.Period, row.Hash, row.Snapshot);
    }

    [Authorize(WorkforcePermissions.PayrollManage), SerializableCommand]
    public virtual async Task<PayslipDto> AttachPayslipAsync(Guid id, PayslipInput input)
    {
        return await InSerializableTransaction<PayslipDto>(async () =>
        {
            var run = await Get(runs, id);
            Require(run.Status == "Approved", "Approved payroll run required.");
            Require(JsonSerializer.Deserialize<List<PayrollRow>>(run.Snapshot)!.Any(x => x.EmployeeId == input.EmployeeId), "Employee does not belong to the approved payroll snapshot.");
            var doc = await Get(documents, input.DocumentId);
            Require(doc.EmployeeId == input.EmployeeId && doc.Kind == "Payslip" && doc.VerifiedBy.HasValue, "Verified payroll-provider payslip required.");
            Require(!await payslips.AnyAsync(x => x.RunId == id && x.EmployeeId == input.EmployeeId), "Payslip already attached.");
            var row = await payslips.InsertAsync(New(new ReleasedPayslip(input.EmployeeId, id, doc.Id)));
            await Audit("Payslip.Attached", row.Id);
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<PayslipDto> ReleasePayslipAsync(Guid id)
    {
        var row = await Get(payslips, id);
        var run = await Get(runs, row.RunId);
        Require(run.Status == "Approved", "Approved payroll required.");
        row.Release();
        await payslips.UpdateAsync(row);
        await Audit("Payslip.Released", id);
        return Dto(row);
    }

    [Authorize(WorkforcePermissions.PayrollOwn)]
    public virtual async Task<List<PayslipDto>> GetPayslipsAsync() => (await payslips.GetListAsync(x => Hr || (x.EmployeeId == Own && x.Released))).Select(Dto).ToList();
    [Authorize(WorkforcePermissions.PayrollOwn)]
    public virtual async Task<DocumentContent> DownloadPayslipAsync(Guid id)
    {
        var row = await Get(payslips, id);
        Allow(Hr || (row.EmployeeId == Own && row.Released));
        var doc = await Get(documents, row.DocumentId);
        await Audit("Payslip.Downloaded", id);
        return new(doc.Name, doc.ContentType, Convert.ToBase64String(doc.Content), doc.Sha256);
    }

    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    static RunDto Dto(PayRun x) => new(x.Id, x.Period, x.Status, x.Hash, x.PreparedBy, x.ReviewedBy, x.ApprovedBy, x.ConcurrencyStamp);
    static PayslipDto Dto(ReleasedPayslip x) => new(x.Id, x.EmployeeId, x.RunId, x.DocumentId, x.Released);
}
