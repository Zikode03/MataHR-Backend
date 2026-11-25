using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
namespace MataHR.Workforce;

public class CalendarService(IRepository<WorkSchedule, Guid> schedules, IRepository<PublicHoliday, Guid> holidays, IRepository<PayRun, Guid> runs) : ITransientDependency
{
    public async Task<int> Days(Guid employee, DateOnly from, DateOnly to)
    {
        var schedule = await schedules.FindAsync(x => x.EmployeeId == employee);
        var days = await holidays.GetListAsync(x => x.Date >= from && x.Date <= to);
        try
        {
            return WorkforceRules.WorkingDays(from, to, schedule?.DaysMask ?? 62, days.Select(x => x.Date).ToHashSet());
        }
        catch (ArgumentException e) { throw new UserFriendlyException(e.Message); }
    }
    public async Task Editable(DateOnly from, DateOnly to)
    {
        var locked = await runs.AnyAsync(x => x.Status == "Approved" && string.Compare(x.Period, from.ToString("yyyy-MM")) >= 0 && string.Compare(x.Period, to.ToString("yyyy-MM")) <= 0);
        if (locked)
            throw new UserFriendlyException("An approved payroll period is locked. Use a separately reviewed adjustment in a later period.");
    }
}
