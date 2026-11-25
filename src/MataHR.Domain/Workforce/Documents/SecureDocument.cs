using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class SecureDocument : WorkforceRow
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Employee";
    public string ContentType { get; set; } = "application/pdf";

    [JsonIgnore]
    public byte[] Content { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public DateOnly? Expires { get; set; }
    public VerificationPurpose Purpose { get; private set; }
    public string VerificationAttestation { get; private set; } = "";
    public DateOnly? SignedOn { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public bool EmployeeVisible { get; set; } = true;

    public void Verify(Guid actor, VerificationPurpose purpose, string attestation, DateOnly? signedOn, DateOnly today)
    {
        DomainGuard.Against(VerifiedBy != null, "VerificationLocked", "Verified evidence is immutable; upload a new document to replace it.");
        DomainGuard.Against(CreatorId == actor, "IndependentVerificationRequired", "A different HR user must verify the document.");
        DomainGuard.Text(attestation, "VerificationAttestation", 2000);
        DomainGuard.Against(purpose == VerificationPurpose.SignedContract && (Kind != "Contract" || signedOn == null || signedOn > today), "InvalidSignatureEvidence", "Signed contract verification requires a valid signature date.");
        DomainGuard.Against(purpose == VerificationPurpose.UnsignedContract && Kind != "Contract" || purpose == VerificationPurpose.Payslip && Kind != "Payslip", "InvalidVerificationPurpose", "Verification purpose does not match the document kind.");
        Purpose = purpose;
        VerificationAttestation = attestation;
        SignedOn = signedOn;
        VerifiedBy = actor;
    }
}
