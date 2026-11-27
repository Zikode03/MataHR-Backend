namespace MataHR.Authentication.Contracts;

public record LoginInput(Guid TenantId, string Email, string Password);

public record AuthAccountDto(Guid Id, Guid TenantId, string Email, string[] Roles, Guid? EmployeeId, string SecurityStamp = "", bool RequiresTwoFactor = false);

public record MfaLoginInput(string Code, bool RecoveryCode = false);

public record MfaSetupInput(string Password);

public record MfaEnableInput(string Code);

public record MfaDisableInput(string Password, string Code);

public record MfaSetupDto(string SharedKey, string AccountName);

public record RecoveryCodesDto(IReadOnlyList<string> Codes);

public record PasswordChangeInput(string CurrentPassword, string NewPassword);

public record PasswordResetRequest(Guid TenantId, string Email);

public record PasswordResetInput(Guid TenantId, string Email, string Token, string NewPassword);
