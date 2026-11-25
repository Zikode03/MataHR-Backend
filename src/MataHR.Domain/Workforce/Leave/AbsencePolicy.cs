namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class AbsencePolicy : WorkforceRow
{
    public string Name { get; private set; } = "";
    public int Version { get; private set; } = 1;
    public bool Active { get; private set; } = true;
    public bool Paid { get; private set; } = true;
    public bool Limited { get; private set; } = true;
    public decimal AnnualDays { get; private set; }
    public bool MonthlyAccrual { get; private set; }
    public decimal CarryLimit { get; private set; }
    public int NoticeDays { get; private set; }
    public int EvidenceAfter { get; private set; }
    public bool RequiresHr { get; private set; }

    protected AbsencePolicy()
    {
    }

    public AbsencePolicy(Guid tenant, string name, int version, bool paid, bool limited, decimal annualDays, bool monthly, decimal carry, int notice, int evidence, bool requiresHr)
    {
        DomainGuard.Against(version < 1 || annualDays < 0 || carry < 0 || notice < 0 || evidence < 0, "InvalidLeavePolicy", "Invalid leave policy.");
        TenantId = tenant;
        Name = DomainGuard.Text(name, "PolicyName", 160);
        Version = version;
        Paid = paid;
        Limited = limited;
        AnnualDays = annualDays;
        MonthlyAccrual = monthly;
        CarryLimit = carry;
        NoticeDays = notice;
        EvidenceAfter = evidence;
        RequiresHr = requiresHr;
    }

    public void Retire() => Active = false;
}
