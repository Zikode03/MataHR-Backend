namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class Worker : WorkforceRow
{
    public string Number { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public DateOnly Started { get; private set; }
    public DateOnly? Exited { get; private set; }
    public string ContractType { get; private set; } = "Permanent";
    public Guid? ManagerId { get; private set; }
    public Guid? PositionId { get; private set; }

    protected Worker()
    {
    }

    public Worker(Guid tenant, string number, string name, string email, DateOnly started, DateOnly? exited = null, string contractType = "Permanent")
    {
        TenantId = tenant;
        ChangeDetails(number, name, email, new EmploymentPeriod(started, exited), contractType);
    }

    public void ChangeDetails(string number, string name, string email, EmploymentPeriod period, string contractType)
    {
        Number = new EmployeeNumber(number).Value;
        Name = DomainGuard.Text(name, "Name", 160);
        DomainGuard.Against(email.Length > 254, "InvalidEmail", "Email is too long.");
        Email = email.Trim();
        Started = period.Start;
        Exited = period.End;
        ContractType = DomainGuard.Text(contractType, "ContractType", 64);
    }

    public void Assign(Guid? position, Guid? manager)
    {
        DomainGuard.Against(manager == Id, "ReportingCycle", "Employee cannot report to themselves.");
        PositionId = position;
        ManagerId = manager;
    }

    public string Snapshot() => System.Text.Json.JsonSerializer.Serialize(new { Id, Number, Name, Email, Started, Exited, ContractType, ManagerId, PositionId });
    public bool IsActive(DateOnly day) => new EmploymentPeriod(Started, Exited).Contains(day);
}
