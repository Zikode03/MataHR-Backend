namespace MataHR.Accounts.Contracts;

public record AuditDto(Guid Id, Guid ActorId, string Action, Guid TargetId, DateTime At, string? Note);

public record ActiveAccountInput(bool Active);

public record AdminOverviewDto(long TotalAccounts, long ActiveAccounts, long InactiveAccounts, long LockedAccounts, IReadOnlyList<RoleCountDto> Roles);

public record RoleCountDto(string Role, long Count);
