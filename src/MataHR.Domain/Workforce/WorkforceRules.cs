namespace MataHR.Workforce;

public static class WorkforceRules
{
    public static DateOnly WorkDate(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime.AddHours(2));
    public static (DateOnly Start, DateOnly End) Period(string period)
    {
        if (!DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd", out var start) || period.Length != 7)
            throw new ArgumentException("Use YYYY-MM period.");
        return (start, start.AddMonths(1).AddDays(-1));
    }
    public static int WorkingDays(DateOnly from, DateOnly to, int mask, IReadOnlySet<DateOnly> holidays)
    {
        if (from > to || to.DayNumber - from.DayNumber > 366)
            throw new ArgumentException("Invalid date range.");
        int days = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        if ((mask & (1 << (int)d.DayOfWeek)) != 0 && !holidays.Contains(d))
            days++;
        return days;
    }
    public static decimal Grant(decimal allocation, decimal carry, decimal adjustment, bool monthly, int year, DateOnly today)
    {
        var months = year < today.Year ? 12 : year > today.Year ? 0 : today.Month;
        return (monthly ? decimal.Floor(allocation * months / 12 * 100) / 100 : allocation) + carry + adjustment;
    }
}
