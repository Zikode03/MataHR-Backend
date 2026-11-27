namespace MataHR.Accounts.Contracts;
public record AccountInput(string Email, string Password, string[] Roles, Guid? EmployeeId);
public record AccountDto(Guid Id, string Email, string[] Roles, Guid? EmployeeId, bool Active, string ConcurrencyStamp);
public record AccountUpdateInput(string[] Roles, Guid? EmployeeId, bool Active, string ConcurrencyStamp, string? Email = null);
public record RolePermissionInput(string Role, string Permission, bool Granted);
public record EmployeeLinkDto(Guid Id, string Number, string Name);
public record RoleGrantDto(string Permission, bool Granted);
