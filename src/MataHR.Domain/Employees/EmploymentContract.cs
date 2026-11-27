using MataHR.Common;
using MataHR.Documents;
using MataHR.Employment;
namespace MataHR.Employees;
[Volo.Abp.Auditing.DisableAuditing]
public class EmploymentContract : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public string Reference { get; private set; } = "";
    public DateOnly Start { get; private set; }
    public DateOnly? End { get; private set; }

    [Volo.Abp.Auditing.Audited]
    public string Status { get; private set; } = "Draft";
    public int Version { get; private set; } = 1;
    public Guid? DocumentId { get; private set; }
    public Guid? SignedDocumentId { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public string UnsignedHash { get; private set; } = "";
    public ContractState State => Enum.Parse<ContractState>(Status);

    protected EmploymentContract()
    {
    }

    public EmploymentContract(Guid tenant, Guid employee, string reference, DateOnly start, DateOnly? end)
    {
        DomainGuard.Against(start == default || end < start, "InvalidContractPeriod", "Invalid contract period.");
        TenantId = tenant;
        EmployeeId = employee;
        Reference = DomainGuard.Text(reference, "ContractReference", 100);
        Start = start;
        End = end;
    }

    public void Transition(ContractState next, Guid actor, string note, SecureDocument? evidence)
    {
        DomainGuard.Against((State, next)is not ((ContractState.Draft, ContractState.InReview) or (ContractState.InReview, ContractState.Approved) or (ContractState.Approved, ContractState.AwaitingSignature) or (ContractState.AwaitingSignature, ContractState.Signed)), "InvalidContractTransition", "Invalid contract transition.");
        DomainGuard.Text(note, "ReviewNote", 2000);
        if (next == ContractState.InReview)
        {
            DomainGuard.Against(evidence == null || evidence.EmployeeId != EmployeeId || evidence.TenantId != TenantId || evidence.Kind != "Contract" || evidence.Purpose != VerificationPurpose.UnsignedContract || evidence.VerifiedBy == null, "UnsignedEvidenceRequired", "Verified unsigned contract evidence required.");
            DocumentId = evidence!.Id;
            UnsignedHash = evidence.Sha256;
            SubmittedBy = actor;
        }

        if (next == ContractState.Approved)
        {
            DomainGuard.Against(SubmittedBy == actor, "IndependentApprovalRequired", "Contract approval must be independent of submission.");
            ApprovedBy = actor;
        }

        if (next == ContractState.Signed)
        {
            DomainGuard.Against(evidence == null || evidence.EmployeeId != EmployeeId || evidence.TenantId != TenantId || evidence.Kind != "Contract" || evidence.Purpose != VerificationPurpose.SignedContract || evidence.VerifiedBy == null || evidence.SignedOn == null || evidence.Id == DocumentId || evidence.Sha256 == UnsignedHash, "SignedEvidenceRequired", "Distinct signed content with independent signature-verification attestation is required.");
            SignedDocumentId = evidence!.Id;
        }

        Status = next.ToString();
    }
}
