namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class EmploymentChange : WorkforceEntity
{
    public Guid EmployeeId { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public string Description { get; private set; } = "";
    public string BeforeStateJson { get; private set; } = "";
    public string AfterStateJson { get; private set; } = "";

    protected EmploymentChange()
    {
    }

    public EmploymentChange(Guid tenant, Guid employee, DateOnly effective, string description, string before, string after)
    {
        TenantId = tenant;
        EmployeeId = employee;
        EffectiveDate = effective;
        Description = DomainGuard.Text(description, "HistoryDescription", 2000);
        BeforeStateJson = before;
        AfterStateJson = after;
    }
}
