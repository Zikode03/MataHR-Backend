using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
public class DashboardAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<WorkSession, Guid> sessions, IRepository<Absence, Guid> leave, IRepository<TimeRequest, Guid> timeRequests, IRepository<PayRun, Guid> runs) : WorkforceService(workers, audits), IDashboardAppService
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
