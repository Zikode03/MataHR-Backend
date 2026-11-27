namespace MataHR.Authentication.Contracts;

// The host uses these operations to validate identities and build HTTP sessions.
public interface IWorkforceAuthenticationService
{
    Task<AuthAccountDto?> LoginAsync(LoginInput input);
    Task<AuthAccountDto> RefreshSessionAsync(Guid tenantId, Guid id);
    Task<AuthAccountDto?> CompleteMfaAsync(Guid tenantId, Guid id, string stamp, string code, bool recovery);
    Task AcknowledgeAsync(Guid accountId);
    Task<CurrentSessionDto> GetCurrentAsync();
}
public record CurrentSessionDto(Guid Id, Guid? TenantId, Guid? EmployeeId, string? Email, string PrivacyVersion, bool PrivacyAcknowledged, string[] Roles, IReadOnlyList<string> Permissions, bool RequiresPasswordChange);
