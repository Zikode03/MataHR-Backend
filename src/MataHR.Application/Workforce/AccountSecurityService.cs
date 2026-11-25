using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
[RemoteService(false)]
public class AccountSecurityService(IdentityUserManager users, ICurrentUser current, ICurrentTenant tenant, IAccountRecoveryDelivery delivery) : ITransientDependency
{
    async Task<Volo.Abp.Identity.IdentityUser> Own() => await users.FindByIdAsync(current.Id?.ToString() ?? "") ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
    static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new UserFriendlyException("Account operation could not be completed. Check the supplied values.");
    }

    [UnitOfWork(true)]
    public virtual async Task<MfaSetupDto> SetupMfaAsync(MfaSetupInput input)
    {
        var user = await Own();
        if (!await users.CheckPasswordAsync(user, input.Password) || await users.GetTwoFactorEnabledAsync(user))
            throw new UserFriendlyException("Verify your password and disable existing MFA before setting up a new authenticator.");
        Check(await users.ResetAuthenticatorKeyAsync(user));
        return new((await users.GetAuthenticatorKeyAsync(user))!, user.Email);
    }

    [UnitOfWork(true)]
    public virtual async Task<RecoveryCodesDto> EnableMfaAsync(MfaEnableInput input)
    {
        var user = await Own();
        if (await users.GetTwoFactorEnabledAsync(user) || !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "")))
            throw new UserFriendlyException("Invalid authenticator code.");
        Check(await users.SetTwoFactorEnabledAsync(user, true));
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        Check(await users.UpdateSecurityStampAsync(user));
        return new(codes!.ToList());
    }

    [UnitOfWork(true)]
    public virtual async Task DisableMfaAsync(MfaDisableInput input)
    {
        var user = await Own();
        if (!await users.CheckPasswordAsync(user, input.Password) || !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "")))
            throw new UserFriendlyException("Password and authenticator verification required.");
        Check(await users.SetTwoFactorEnabledAsync(user, false));
        Check(await users.ResetAuthenticatorKeyAsync(user));
        Check(await users.UpdateSecurityStampAsync(user));
    }

    [UnitOfWork(true)]
    public virtual async Task ChangePasswordAsync(PasswordChangeInput input)
    {
        var user = await Own();
        Check(await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword));
        Check(await users.UpdateSecurityStampAsync(user));
    }

    [UnitOfWork(true)]
    public virtual async Task RequestResetAsync(PasswordResetRequest input)
    {
        if (!delivery.IsConfigured)
            throw new UserFriendlyException("Account recovery email is not configured.");
        using (tenant.Change(input.TenantId))
        {
            var user = await users.FindByEmailAsync(input.Email);
            if (user != null && user.IsActive)
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                await delivery.SendAsync(input.TenantId, user.Email, token);
            }
        }
    }

    [UnitOfWork(true)]
    public virtual async Task ResetPasswordAsync(PasswordResetInput input)
    {
        using (tenant.Change(input.TenantId))
        {
            var user = await users.FindByEmailAsync(input.Email);
            if (user == null || !user.IsActive)
                throw new UserFriendlyException("Invalid or expired reset request.");
            Check(await users.ResetPasswordAsync(user, input.Token, input.NewPassword));
            Check(await users.SetLockoutEndDateAsync(user, null));
            Check(await users.ResetAccessFailedCountAsync(user));
            Check(await users.UpdateSecurityStampAsync(user));
        }
    }
}
