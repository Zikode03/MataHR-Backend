using MataHR.Common;
namespace MataHR.Employees;

// The exit date is the first day outside active employment.
public readonly record struct EmploymentPeriod
{
    public DateOnly Start { get; }
    public DateOnly? End { get; }

    public EmploymentPeriod(DateOnly start, DateOnly? end)
    {
        DomainGuard.Against(start == default || end < start, "InvalidEmploymentPeriod", "Employment end cannot precede start.");
        Start = start;
        End = end;
    }

    public bool Contains(DateOnly day) => day >= Start && (!End.HasValue || day < End);
}
