using MataHR.Imports.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace MataHR.HttpApi;

// Review and commit are separate requests so the caller can inspect the source before any records are written.
[Authorize, ApiController, Route("api/imports")]
public class LegacyImportController(ILegacyImportAppService imports) : AbpController
{
    [HttpPost("preview")]
    public Task<LegacyPreviewDto> Preview(LegacyPreviewInput input) => imports.PreviewAsync(input);
    [HttpPost("{id:guid}/commit")]
    public Task<LegacyCommitDto> Commit(Guid id, LegacyCommitInput input) => imports.CommitAsync(id, input);
}
