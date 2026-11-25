using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using MataHR.Workforce.Contracts;
namespace MataHR.Workforce;

public class DocumentsAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<SecureDocument, Guid> documents) : WorkforceService(workers, audits), IDocumentsAppService
{
    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<List<DocumentDto>> GetListAsync(Guid employeeId)
    {
        await View(employeeId, true);
        return (await documents.GetListAsync(x => x.EmployeeId == employeeId && (Hr || x.EmployeeVisible) && x.Kind != "Payslip")).Select(Dto).ToList();
    }
    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<DocumentDto> UploadAsync(DocumentInput input)
    {
        await View(input.EmployeeId, true);
        Require(input.Kind is "Employee" or "Contract" or "LeaveEvidence" or "Payslip", "Invalid document kind.");
        Allow(Hr || input.Kind is "Employee" or "LeaveEvidence");
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 180 && Path.GetFileName(input.Name) == input.Name, "Invalid filename.");
        Require(input.ContentBase64.Length <= 7 * 1024 * 1024, "Document exceeds 5 MB.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.ContentBase64);
        }
        catch (FormatException) { throw new Volo.Abp.UserFriendlyException("Invalid document encoding."); }
        Require(bytes.Length is > 0 and <= 5 * 1024 * 1024, "Document exceeds 5 MB.");
        var valid = input.ContentType switch
        {
            "application/pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
            "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.Length > 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            _ => false
        };
        Require(valid, "Only valid PDF, PNG or JPEG documents are accepted.");
        var row = New(new SecureDocument { EmployeeId = input.EmployeeId, Name = input.Name, Kind = input.Kind, ContentType = input.ContentType, Content = bytes, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), Expires = input.Expires, EmployeeVisible = Hr ? input.EmployeeVisible : true });
        await documents.InsertAsync(row);
        await Audit("Document.Uploaded", row.Id);
        await Flush();
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PeopleWrite)]
    public virtual async Task<DocumentDto> VerifyAsync(Guid id)
    {
        var row = await Get(documents, id);
        Require(row.CreatorId != Actor, "A different HR user must verify the document.");
        row.VerifiedBy = Actor;
        await documents.UpdateAsync(row);
        await Audit("Document.Verified", id);
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<DocumentContent> DownloadAsync(Guid id)
    {
        var row = await Get(documents, id);
        await View(row.EmployeeId, true);
        Allow(Hr || (row.EmployeeVisible && row.Kind != "Payslip"));
        await Audit("Document.Downloaded", id);
        return new(row.Name, row.ContentType, Convert.ToBase64String(row.Content), row.Sha256);
    }
    static DocumentDto Dto(SecureDocument x) => new(x.Id, x.EmployeeId, x.Name, x.Kind, x.ContentType, x.Content.Length, x.Sha256, x.Expires, x.VerifiedBy.HasValue);
}
