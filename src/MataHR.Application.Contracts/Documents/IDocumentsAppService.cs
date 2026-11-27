using Volo.Abp.Application.Services;
namespace MataHR.Documents.Contracts;
public interface IDocumentsAppService : IApplicationService
{
    Task<List<DocumentDto>> GetListAsync(Guid employeeId);
    Task<DocumentDto> UploadAsync(DocumentInput input);
    Task<DocumentDto> VerifyAsync(Guid id, DocumentVerificationInput input);
    Task<DocumentContent> DownloadAsync(Guid id);
}
