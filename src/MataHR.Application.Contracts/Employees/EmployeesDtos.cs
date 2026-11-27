namespace MataHR.Employees.Contracts;
public record EmployeeDto(Guid Id, string Number, string Name, string Email, DateOnly Started, DateOnly? Exited, string ContractType, Guid? ManagerId, Guid? PositionId, string ConcurrencyStamp);
public record EmployeeInput(string Number, string Name, string Email, DateOnly Started, DateOnly? Exited, string ContractType, Guid? ManagerId, Guid? PositionId, string? ConcurrencyStamp = null);
public record EmployeeHistoryDto(Guid Id, DateOnly EffectiveDate, string Description, string BeforeStateJson = "", string AfterStateJson = "");
public record ContractDto(Guid Id, Guid EmployeeId, string Reference, DateOnly Start, DateOnly? End, string Status, int Version, Guid? DocumentId, Guid? SignedDocumentId, string ConcurrencyStamp);
public record ContractInput(Guid EmployeeId, string Reference, DateOnly Start, DateOnly? End);
public record ContractDecision(string Status, string Note, Guid? DocumentId, string ConcurrencyStamp);
