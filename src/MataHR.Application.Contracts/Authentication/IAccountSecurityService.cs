namespace MataHR.Authentication.Contracts;
public interface IAccountSecurityService
{
    Task<MfaSetupDto> SetupMfaAsync(MfaSetupInput input);
    Task<RecoveryCodesDto> EnableMfaAsync(MfaEnableInput input);
    Task DisableMfaAsync(MfaDisableInput input);
    Task ChangePasswordAsync(PasswordChangeInput input);
    Task RequestResetAsync(PasswordResetRequest input);
    Task ResetPasswordAsync(PasswordResetInput input);
}
