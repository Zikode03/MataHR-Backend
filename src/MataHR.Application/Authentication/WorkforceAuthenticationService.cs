using MataHR.Authentication.Contracts;
using MataHR.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace MataHR.Authentication;

// Resolve accounts in the requested tenant before issuing a session. A matching email alone does not identify the organisation.
[RemoteService(false)]
public class WorkforceAuthenticationService(IRepository<PrivacyAcceptance, Guid> privacy, Volo.Abp.Identity.IdentityUserManager users, ICurrentTenant tenant, IUnitOfWorkManager units, Volo.Abp.Users.ICurrentUser current, Volo.Abp.Authorization.Permissions.IPermissionChecker permissionChecker) : IWorkforceAuthenticationService, ITransientDependency
{
    public const string PrivacyVersion = WorkforcePrivacy.Version;
    [UnitOfWork(true)]
    public virtual async Task<AuthAccountDto?> LoginAsync(LoginInput input)
    {
        if (input.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(input.Email) || input.Password == null)
            return null;
        using (tenant.Change(input.TenantId))
        {
            var email = input.Email.Trim().ToLowerInvariant();
            var account = await users.FindByEmailAsync(email);
            if (account == null || !account.IsActive || await users.IsLockedOutAsync(account))
                return null;
            if (!await users.CheckPasswordAsync(account, input.Password))
            {
                await users.AccessFailedAsync(account);
                await units.Current!.SaveChangesAsync();
                return null;
            }

            await users.ResetAccessFailedCountAsync(account);
            var requiresMfa = await users.GetTwoFactorEnabledAsync(account);
            return new(account.Id, account.TenantId!.Value, account.Email, (await users.GetRolesAsync(account)).Order().ToArray(), account.GetProperty<Guid?>("EmployeeId"), account.SecurityStamp, requiresMfa);
        }
    }

    [UnitOfWork]
    public virtual async Task<AuthAccountDto> RefreshSessionAsync(Guid tenantId, Guid id)
    {
        using (tenant.Change(tenantId))
        {
            var user = await users.FindByIdAsync(id.ToString());
            if (user == null || !user.IsActive)
                throw new Volo.Abp.Authorization.AbpAuthorizationException();
            return new(user.Id, tenantId, user.Email, (await users.GetRolesAsync(user)).Order().ToArray(), user.GetProperty<Guid?>("EmployeeId"), user.SecurityStamp);
        }
    }

    [UnitOfWork(true)]
    public virtual async Task<AuthAccountDto?> CompleteMfaAsync(Guid tenantId, Guid id, string stamp, string code, bool recovery)
    {
        using (tenant.Change(tenantId))
        {
            var user = await users.FindByIdAsync(id.ToString());
            if (user == null || !user.IsActive || user.SecurityStamp != stamp || !await users.GetTwoFactorEnabledAsync(user) || await users.IsLockedOutAsync(user))
                return null;
            bool valid = recovery ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, code)).Succeeded : await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code.Replace(" ", ""));
            if (!valid)
            {
                await users.AccessFailedAsync(user);
                return null;
            }

            await users.ResetAccessFailedCountAsync(user);
            return new(user.Id, tenantId, user.Email, (await users.GetRolesAsync(user)).Order().ToArray(), user.GetProperty<Guid?>("EmployeeId"), user.SecurityStamp);
        }
    }

    [UnitOfWork(true)]
    public virtual async Task AcknowledgeAsync(Guid accountId)
    {
        if (!await privacy.AnyAsync(x => x.AccountId == accountId && x.Version == PrivacyVersion))
            await privacy.InsertAsync(new PrivacyAcceptance { TenantId = tenant.Id, AccountId = accountId, Version = PrivacyVersion });
    }

    [Authorize, UnitOfWork]
    public virtual async Task<CurrentSessionDto> GetCurrentAsync()
    {
        var id = current.Id ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
        var account = await users.FindByIdAsync(id.ToString()) ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
        var granted = new List<string>();
        foreach (var permission in WorkforcePermissions.All)
            if (await permissionChecker.IsGrantedAsync(permission)) granted.Add(permission);
        return new(id, tenant.Id, account.GetProperty<Guid?>("EmployeeId"), account.Email, PrivacyVersion,
            await privacy.AnyAsync(x => x.AccountId == id && x.Version == PrivacyVersion),
            current.Roles, granted, account.GetProperty<bool>("RequirePasswordChange"));
    }
}
