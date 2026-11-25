namespace MataHR.Workforce.Contracts;
public record LoginInput(Guid TenantId, string Email, string Password);
public record AuthAccountDto(Guid Id, Guid TenantId, string Email, string[] Roles, Guid? EmployeeId, string SecurityStamp = "", bool RequiresTwoFactor = false);
public record AuditDto(Guid Id, Guid ActorId, string Action, Guid TargetId, DateTime At, string? Note);
public record ActiveAccountInput(bool Active);
public record MfaLoginInput(string Code, bool RecoveryCode = false);
public record MfaSetupInput(string Password);
public record MfaEnableInput(string Code);
public record MfaDisableInput(string Password, string Code);
public record MfaSetupDto(string SharedKey, string AccountName);
public record RecoveryCodesDto(IReadOnlyList<string> Codes);
public record PasswordChangeInput(string CurrentPassword, string NewPassword);
public record PasswordResetRequest(Guid TenantId, string Email);
public record PasswordResetInput(Guid TenantId, string Email, string Token, string NewPassword);
public interface IAccountRecoveryDelivery
{
    bool IsConfigured { get; }

    Task SendAsync(Guid tenantId, string email, string token);
}
