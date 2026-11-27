namespace MataHR.Documents.Contracts;
public record DocumentInput(Guid EmployeeId, string Name, string ContentType, string ContentBase64, string Kind = "Employee", bool EmployeeVisible = true, DateOnly? Expires = null);
public record DocumentDto(Guid Id, Guid EmployeeId, string Name, string Kind, string ContentType, int Size, string Sha256, DateOnly? Expires, bool Verified);
public record DocumentContent(string Name, string ContentType, string ContentBase64, string Sha256);
public record DocumentVerificationInput(string Purpose = "General", string Attestation = "", DateOnly? SignedOn = null);
