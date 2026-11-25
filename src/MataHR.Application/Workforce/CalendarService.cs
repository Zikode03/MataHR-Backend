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
        catch (ArgumentException e)
        {
            throw new UserFriendlyException(e.Message);
        }
    }

    public async Task<List<DateOnly>> WorkingDates(Guid employee, DateOnly from, DateOnly to)
    {
        if (from > to || to.DayNumber - from.DayNumber > 366)
            throw new UserFriendlyException("Invalid leave period.");
        var schedule = await schedules.FindAsync(x => x.EmployeeId == employee);
        var days = (await holidays.GetListAsync(x => x.Date >= from && x.Date <= to)).Select(x => x.Date).ToHashSet();
        var mask = schedule?.DaysMask ?? 62;
        var result = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if ((mask & (1 << (int)d.DayOfWeek)) != 0 && !days.Contains(d))
                result.Add(d);
        return result;
    }

    public async Task EffectiveChangeAllowed(DateOnly effective)
    {
        if (await runs.AnyAsync(x => x.Status == "Approved" && string.Compare(x.Period, effective.ToString("yyyy-MM")) >= 0))
            throw new UserFriendlyException("An approved payroll period would be changed by this effective date.");
    }

    public async Task Editable(DateOnly from, DateOnly to)
    {
        var locked = await runs.AnyAsync(x => x.Status == "Approved" && string.Compare(x.Period, from.ToString("yyyy-MM")) >= 0 && string.Compare(x.Period, to.ToString("yyyy-MM")) <= 0);
        if (locked)
            throw new UserFriendlyException("An approved payroll period is locked. Use a separately reviewed adjustment in a later period.");
    }
}
