using MataHR.Common;
namespace MataHR.Imports;

// A review is tied to its source fingerprint and expiry so stale previews cannot be committed.
[Volo.Abp.Auditing.DisableAuditing]
public class LegacyImportBatch : WorkforceRow
{
    public string Kind { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public Guid PreparedBy { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }

    protected LegacyImportBatch()
    {
    }

    public LegacyImportBatch(Guid tenant, string kind, string fingerprint, Guid actor, DateTimeOffset now)
    {
        TenantId = tenant;
        Kind = kind;
        Fingerprint = fingerprint;
        PreparedBy = actor;
        ExpiresAt = now.AddMinutes(15);
    }

    public void Commit(Guid actor, DateTimeOffset now)
    {
        DomainGuard.Against(actor != PreparedBy || now > ExpiresAt || CommittedAt.HasValue, "InvalidImportReview", "This review expired, was already used, or belongs to another user.");
        CommittedAt = now;
    }
}
