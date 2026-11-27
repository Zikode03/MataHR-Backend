namespace MataHR.Imports.Contracts;
public record LegacyPreviewInput(string Kind);
public record LegacyCommitInput(string Fingerprint, bool Confirmed);
public record LegacyReviewRow(Guid SourceId, string Label, string Status, string Detail);
public record LegacyPreviewDto(Guid Id, string Kind, DateTimeOffset ExpiresAt, string Fingerprint, IReadOnlyList<LegacyReviewRow> Rows, bool CanCommit, bool Committed, Guid SourceTenantId, Guid TargetTenantId);
public record TemporaryCredential(string Email, string TemporaryPassword);
public record LegacyCommitDto(int Imported, int Skipped, IReadOnlyList<TemporaryCredential> Credentials);
