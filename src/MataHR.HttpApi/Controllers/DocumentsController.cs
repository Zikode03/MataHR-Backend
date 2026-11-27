using MataHR.Documents.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;

namespace MataHR.HttpApi;

// Keep the API boundary focused on requests and responses. Document access and verification are checked by the service.
[Authorize, Route("api/documents"), ApiController]
public class DocumentsController(IDocumentsAppService service) : AbpController
{
    [HttpGet("")]
    public Task<List<DocumentDto>> Get(Guid employeeId) => service.GetListAsync(employeeId);
    [HttpPost("")]
    public Task<DocumentDto> Upload(DocumentInput input) => service.UploadAsync(input);
    [HttpPost("{id:guid}/verify")]
    public Task<DocumentDto> Verify(Guid id, DocumentVerificationInput input) => service.VerifyAsync(id, input);
    [HttpGet("{id:guid}/download")]
    public Task<DocumentContent> Download(Guid id) => service.DownloadAsync(id);
}
