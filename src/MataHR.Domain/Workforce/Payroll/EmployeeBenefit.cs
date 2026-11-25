namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class EmployeeBenefit : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Employer { get; private set; }
    public decimal EmployeeCost { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool Active => EffectiveTo == null;

    protected EmployeeBenefit()
    {
    }

    public EmployeeBenefit(Guid tenant, Guid employee, string name, decimal employer, decimal employeeCost, DateOnly effectiveFrom, DateOnly? effectiveTo = null)
    {
        DomainGuard.Against(effectiveFrom == default || effectiveFrom.Day != 1 || effectiveTo < effectiveFrom || (effectiveTo.HasValue && effectiveTo.Value.Day != 1), "InvalidBenefitDates", "Benefits use first-of-month effective dates and an exclusive end date.");
        TenantId = tenant;
        EmployeeId = employee;
        Name = DomainGuard.Text(name, "BenefitName", 160);
        Employer = new Money(employer).Amount;
        EmployeeCost = new Money(employeeCost).Amount;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }
}
