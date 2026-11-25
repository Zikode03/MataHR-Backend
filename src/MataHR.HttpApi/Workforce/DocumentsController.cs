using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;
namespace MataHR.HttpApi;

[Authorize, Route("api/documents"), ApiController]
public class DocumentsController(IDocumentsAppService service) : AbpController
{
    [HttpGet("")] public Task<List<DocumentDto>> Get(Guid employeeId) => service.GetListAsync(employeeId);
    [HttpPost("")] public Task<DocumentDto> Upload(DocumentInput input) => service.UploadAsync(input);
    [HttpPost("{id:guid}/verify")] public Task<DocumentDto> Verify(Guid id) => service.VerifyAsync(id);
    [HttpGet("{id:guid}/download")] public Task<DocumentContent> Download(Guid id) => service.DownloadAsync(id);
}
