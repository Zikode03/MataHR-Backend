namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class Compensation : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal Basic { get; private set; }
    public decimal Allowance { get; private set; }
    public decimal Deduction { get; private set; }
    public decimal HourlyRate { get; private set; }
    public decimal OvertimeMultiplier { get; private set; } = 1.5m;

    protected Compensation()
    {
    }

    public Compensation(Guid tenant, Guid employee, DateOnly effectiveFrom, decimal basic, decimal allowance, decimal deduction, decimal hourlyRate, decimal multiplier)
    {
        DomainGuard.Against(effectiveFrom == default || effectiveFrom.Day != 1, "InvalidEffectiveDate", "Compensation changes must have an explicit first-of-month effective date. Mid-month proration is not configured.");
        DomainGuard.Against(multiplier < 1 || multiplier > 3, "InvalidOvertimeMultiplier", "Overtime multiplier must be between 1 and 3.");
        TenantId = tenant;
        EmployeeId = employee;
        EffectiveFrom = effectiveFrom;
        Basic = new Money(basic).Amount;
        Allowance = new Money(allowance).Amount;
        Deduction = new Money(deduction).Amount;
        HourlyRate = new Money(hourlyRate).Amount;
        OvertimeMultiplier = multiplier;
    }
}
