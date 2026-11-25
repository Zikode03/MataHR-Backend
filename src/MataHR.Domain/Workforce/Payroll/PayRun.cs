using System.Security.Cryptography;
using System.Text;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class PayRun : WorkforceRow
{
    public string Period { get; private set; } = "";

    [Volo.Abp.Auditing.Audited]
    public string Status { get; private set; } = "Draft";
    public string Snapshot { get; private set; } = "[]";
    public string Hash { get; private set; } = "";
    public Guid? PreparedBy { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }

    protected PayRun()
    {
    }

    public PayRun(Guid tenant, string period)
    {
        WorkforceRules.Period(period);
        TenantId = tenant;
        Period = period;
    }

    public void Prepare(string snapshot, Guid actor)
    {
        DomainGuard.Against(Status != "Draft", "PayrollLocked", "Reviewed or approved payroll is locked.");
        DomainGuard.Against(string.IsNullOrWhiteSpace(snapshot) || snapshot == "[]", "EmptyPayroll", "Payroll inputs are required.");
        Snapshot = snapshot;
        Hash = Fingerprint(snapshot);
        PreparedBy = actor;
    }

    public void Transition(PayRunState next, Guid actor, string note)
    {
        DomainGuard.Text(note, "ReviewNote", 2000);
        DomainGuard.Against((Status, next)is not (("Draft", PayRunState.Reviewed) or ("Reviewed", PayRunState.Approved) or ("Reviewed", PayRunState.Draft)), "InvalidPayrollTransition", "Invalid payroll transition.");
        if (next == PayRunState.Reviewed)
            ReviewedBy = actor;
        if (next == PayRunState.Approved)
        {
            DomainGuard.Against(ReviewedBy == actor || PreparedBy == actor, "IndependentPayrollApprovalRequired", "Payroll approval must be independent of preparation and review.");
            ApprovedBy = actor;
        }

        if (next == PayRunState.Draft)
        {
            ReviewedBy = null;
            ApprovedBy = null;
        }

        Status = next.ToString();
    }

    public static string Fingerprint(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
