using Volo.Abp.Application.Services;

namespace MataHR.Imports.Contracts;

// The preview gives the caller a snapshot to review. Commit accepts only that reviewed snapshot.
public interface ILegacyImportAppService : IApplicationService
{
    Task<LegacyPreviewDto> PreviewAsync(LegacyPreviewInput input);
    Task<LegacyCommitDto> CommitAsync(Guid id, LegacyCommitInput input);
}
