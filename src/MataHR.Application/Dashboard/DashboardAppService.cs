using MataHR.Attendance;
using MataHR.Auditing;
using MataHR.Employees;
using MataHR.Leave;
using MataHR.Payroll;
using MataHR.Common;
using MataHR.Dashboard.Contracts;
using MataHR.Payroll.Contracts;
using MataHR.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace MataHR.Dashboard;

// Compute the summary from the caller’s visible workforce so dashboard totals respect the same scope as the records.
public class DashboardAppService(IRepository<Employee, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<WorkSession, Guid> sessions, IRepository<LeaveRequest, Guid> leave, IRepository<TimeRequest, Guid> timeRequests, IRepository<PayRun, Guid> runs) : WorkforceService(workers, audits), IDashboardAppService
{
    [Authorize(WorkforcePermissions.Dashboard)]
    public virtual async Task<DashboardDto> GetAsync(DateOnly from, DateOnly to)
    {
        Require(from <= to && to.DayNumber - from.DayNumber <= 366, "Invalid dashboard period.");
        var people = await Queries.ToListAsync(await Visible());
        var ids = people.Select(x => x.Id).ToArray();
        var ss = await sessions.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Date >= from && x.Date <= to);
        var ls = await leave.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Start <= to && x.End >= from);
        var ts = await timeRequests.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Date >= from && x.Date <= to);
        decimal? payroll = null;
        if (Hr)
        {
            var run = await runs.FindAsync(x => x.Period == to.ToString("yyyy-MM"));
            if (run != null)
                payroll = System.Text.Json.JsonSerializer.Deserialize<List<PayrollRow>>(run.Snapshot)!.Sum(x => x.Basic + x.Allowance + x.OvertimePay + x.EmployerBenefits);
        }

        return new(from, to, people.Count(x => Active(x, to)), people.Count(x => x.Started >= from && x.Started <= to), ss.Where(x => x.Date == Today && x.Status != "Rejected").Select(x => x.EmployeeId).Distinct().Count(), ls.Where(x => x.Status == "Approved" && x.Start <= Today && x.End >= Today).Select(x => x.EmployeeId).Distinct().Count(), ls.Count(x => x.Status is "Pending" or "Cancellation pending"), ss.Count(x => x.Status == "Pending review"), ts.Count(x => x.Status == "Pending"), payroll);
    }
}
