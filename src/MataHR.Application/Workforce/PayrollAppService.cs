using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;
namespace MataHR.Workforce;

public class PayrollAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<Compensation, Guid> salaries, IRepository<EmployeeBenefit, Guid> benefits, IRepository<WorkSession, Guid> sessions, IRepository<TimeRequest, Guid> timeRequests, IRepository<Absence, Guid> leave, IRepository<PayRun, Guid> runs, IRepository<ReleasedPayslip, Guid> payslips, IRepository<SecureDocument, Guid> documents, CalendarService calendar) : WorkforceService(workers, audits), IPayrollAppService
{
    [Authorize(WorkforcePermissions.PayrollManage)] public virtual async Task<List<CompensationDto>> GetSalariesAsync() => (await salaries.GetListAsync()).Select(x => new CompensationDto(x.EmployeeId, x.Basic, x.Allowance, x.Deduction, x.HourlyRate, x.OvertimeMultiplier)).ToList();
    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task SetSalaryAsync(CompensationInput input)
    {
        await Worker(input.EmployeeId);
        Require(input.Basic >= 0 && input.Allowance >= 0 && input.Deduction >= 0 && input.HourlyRate >= 0 && input.OvertimeMultiplier is >= 1 and <= 3, "Invalid compensation.");
        await calendar.Editable(new DateOnly(Today.Year, Today.Month, 1), Today);
        var row = await salaries.FindAsync(x => x.EmployeeId == input.EmployeeId);
        if (row == null)
            row = await salaries.InsertAsync(New(new Compensation { EmployeeId = input.EmployeeId }));
        row.Basic = input.Basic;
        row.Allowance = input.Allowance;
        row.Deduction = input.Deduction;
        row.HourlyRate = input.HourlyRate;
        row.OvertimeMultiplier = input.OvertimeMultiplier;
        await salaries.UpdateAsync(row);
        await Audit("Compensation.Updated", row.Id);
    }
    [Authorize(WorkforcePermissions.PayrollOwn)]
    public virtual async Task<List<BenefitDto>> GetBenefitsAsync()
    {
        var q = await benefits.GetQueryableAsync();
        return (await Queries.ToListAsync(q.Where(x => Hr || x.EmployeeId == Own))).Select(x => new BenefitDto(x.Id, x.EmployeeId, x.Name, x.Employer, x.EmployeeCost, x.Active)).ToList();
    }
    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<BenefitDto> CreateBenefitAsync(BenefitInput input)
    {
        await Worker(input.EmployeeId);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Employer >= 0 && input.EmployeeCost >= 0, "Invalid benefit.");
        await calendar.Editable(new DateOnly(Today.Year, Today.Month, 1), Today);
        var row = await benefits.InsertAsync(New(new EmployeeBenefit { EmployeeId = input.EmployeeId, Name = input.Name, Employer = input.Employer, EmployeeCost = input.EmployeeCost }));
        await Audit("Benefit.Created", row.Id);
        return new(row.Id, row.EmployeeId, row.Name, row.Employer, row.EmployeeCost, row.Active);
    }
    [Authorize(WorkforcePermissions.PayrollManage)] public virtual async Task<PayrollReadiness> GetReadinessAsync(string period) => await Readiness(period);
    async Task<PayrollReadiness> Readiness(string period)
    {
        DateOnly start, end;
        try
        {
            (start, end) = WorkforceRules.Period(period);
        }
        catch (ArgumentException e) { throw new Volo.Abp.UserFriendlyException(e.Message); }
        var people = await workers.GetListAsync(x => x.Started <= end && (x.Exited == null || x.Exited > start));
        var issues = new List<string>();
        var rows = new List<PayrollRow>();
        if (people.Count == 0)
            issues.Add("No active employees in this period.");
        foreach (var employee in people.OrderBy(x => x.Id))
        {
            var pay = await salaries.FindAsync(x => x.EmployeeId == employee.Id);
            if (pay == null)
            {
                issues.Add("Missing compensation: " + employee.Number);
                continue;
            }
            var bs = await benefits.GetListAsync(x => x.EmployeeId == employee.Id && x.Active);
            var ss = await sessions.GetListAsync(x => x.EmployeeId == employee.Id && x.Date >= start && x.Date <= end);
            var ts = await timeRequests.GetListAsync(x => x.EmployeeId == employee.Id && x.Date >= start && x.Date <= end);
            var ls = await leave.GetListAsync(x => x.EmployeeId == employee.Id && x.Start <= end && x.End >= start);
            var pending = ss.Count(x => x.Status is "Open" or "Pending review") + ts.Count(x => x.Status == "Pending") + ls.Count(x => x.Status is "Pending" or "Cancellation pending");
            if (pending > 0)
                issues.Add("Resolve pending attendance, overtime or leave: " + employee.Number);
            decimal hours = decimal.Round((decimal)ss.Where(x => x.Status == "Approved" && x.End.HasValue).Sum(x => (x.End!.Value - x.Start).TotalHours), 2);
            decimal overtime = decimal.Round((decimal)ts.Where(x => x.Kind == "Overtime" && x.Status == "Approved").Sum(x => (x.End - x.Start).TotalHours), 2);
            if (overtime > 0 && pay.HourlyRate == 0)
                issues.Add("Overtime hourly rate missing: " + employee.Number);
            int unpaid = 0;
            foreach (var l in ls.Where(x => x.Status == "Approved" && !x.Paid))
                unpaid += await calendar.Days(employee.Id, l.Start > start ? l.Start : start, l.End < end ? l.End : end);
            rows.Add(new(employee.Id, employee.Number, pay.Basic, pay.Allowance, pay.Deduction, bs.Sum(x => x.Employer), bs.Sum(x => x.EmployeeCost), hours, overtime, decimal.Round(overtime * pay.HourlyRate * pay.OvertimeMultiplier, 2, MidpointRounding.AwayFromZero), unpaid, pending));
        }
        return new(period, issues.Count == 0, issues, rows);
    }
    [Authorize(WorkforcePermissions.PayrollManage)] public virtual async Task<List<RunDto>> GetRunsAsync() => (await runs.GetListAsync()).OrderByDescending(x => x.Period).Select(Dto).ToList();
    [Authorize(WorkforcePermissions.PayrollManage), UnitOfWork(true)]
    public virtual async Task<RunDto> PrepareAsync(RunInput input)
    {
        var readiness = await Readiness(input.Period);
        Require(readiness.Rows.Count > 0, "No complete payroll inputs.");
        var row = await runs.FindAsync(x => x.Period == input.Period);
        if (row == null)
            row = await runs.InsertAsync(New(new PayRun { Period = input.Period }));
        Require(row.Status == "Draft", "Reviewed or approved payroll is locked.");
        row.Snapshot = JsonSerializer.Serialize(readiness.Rows);
        row.Hash = Hash(row.Snapshot);
        row.PreparedBy = Actor;
        await runs.UpdateAsync(row);
        await Audit("Payroll.Prepared", row.Id);
        await Flush();
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PayrollManage), UnitOfWork(true)]
    public virtual async Task<RunDto> TransitionAsync(Guid id, DecisionInput input)
    {
        var row = await Get(runs, id);
        Stamp(row, input.ConcurrencyStamp);
        Require(!string.IsNullOrWhiteSpace(input.Note) && (row.Status, input.Status) is ("Draft", "Reviewed") or ("Reviewed", "Approved") or ("Reviewed", "Draft"), "Invalid payroll transition or missing review note.");
        if (input.Status != "Draft")
        {
            var live = await Readiness(row.Period);
            Require(live.Ready && Hash(JsonSerializer.Serialize(live.Rows)) == row.Hash, "Payroll inputs changed or need attention. Regenerate the draft.");
        }
        if (input.Status == "Reviewed")
            row.ReviewedBy = Actor;
        if (input.Status == "Approved")
        {
            Allow(row.ReviewedBy != Actor);
            row.ApprovedBy = Actor;
        }
        if (input.Status == "Draft")
        {
            row.ReviewedBy = null;
            row.ApprovedBy = null;
        }
        row.Status = input.Status;
        await runs.UpdateAsync(row);
        await Audit("Payroll." + input.Status, id, input.Note);
        await Flush();
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<ExportDto> ExportAsync(Guid id)
    {
        var row = await Get(runs, id);
        Require(row.Status == "Approved", "Only approved payroll inputs can be exported.");
        await Audit("Payroll.Exported", id);
        return new(row.Period, row.Hash, row.Snapshot);
    }
    [Authorize(WorkforcePermissions.PayrollManage), UnitOfWork(true)]
    public virtual async Task<PayslipDto> AttachPayslipAsync(Guid id, PayslipInput input)
    {
        var run = await Get(runs, id);
        Require(run.Status == "Approved", "Approved payroll run required.");
        Require(JsonSerializer.Deserialize<List<PayrollRow>>(run.Snapshot)!.Any(x => x.EmployeeId == input.EmployeeId), "Employee does not belong to the approved payroll snapshot.");
        var doc = await Get(documents, input.DocumentId);
        Require(doc.EmployeeId == input.EmployeeId && doc.Kind == "Payslip" && doc.VerifiedBy.HasValue, "Verified payroll-provider payslip required.");
        Require(!await payslips.AnyAsync(x => x.RunId == id && x.EmployeeId == input.EmployeeId), "Payslip already attached.");
        var row = await payslips.InsertAsync(New(new ReleasedPayslip { EmployeeId = input.EmployeeId, RunId = id, DocumentId = doc.Id }));
        await Audit("Payslip.Attached", row.Id);
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PayrollManage)]
    public virtual async Task<PayslipDto> ReleasePayslipAsync(Guid id)
    {
        var row = await Get(payslips, id);
        var run = await Get(runs, row.RunId);
        Require(run.Status == "Approved", "Approved payroll required.");
        row.Released = true;
        await payslips.UpdateAsync(row);
        await Audit("Payslip.Released", id);
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PayrollOwn)] public virtual async Task<List<PayslipDto>> GetPayslipsAsync() => (await payslips.GetListAsync(x => Hr || (x.EmployeeId == Own && x.Released))).Select(Dto).ToList();
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
