namespace MataHR.Workforce.Contracts;

public record LoginInput(Guid TenantId, string Email, string Password);
public record AuthAccountDto(Guid Id, Guid TenantId, string Email, string[] Roles, Guid? EmployeeId);
public record AuditDto(Guid Id, Guid ActorId, string Action, Guid TargetId, DateTime At, string? Note);
public record ActiveAccountInput(bool Active);
