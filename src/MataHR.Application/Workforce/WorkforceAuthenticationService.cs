using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;
namespace MataHR.Workforce;

[RemoteService(false)]
public class WorkforceAuthenticationService(IRepository<WorkforceUser, Guid> accounts, IRepository<PrivacyAcceptance, Guid> privacy, IPasswordHasher<WorkforceUser> hasher, ICurrentTenant tenant, IUnitOfWorkManager units) : ITransientDependency
{
    public const string PrivacyVersion = "workforce-1.0";
    [UnitOfWork(true)]
    public virtual async Task<AuthAccountDto?> LoginAsync(LoginInput input)
    {
        if (input.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(input.Email) || input.Password == null)
            return null;
        using (tenant.Change(input.TenantId))
        {
            var email = input.Email.Trim().ToLowerInvariant();
            var account = await accounts.FindAsync(x => x.Email == email && x.Active);
            if (account == null || account.LockedUntil > DateTimeOffset.UtcNow)
                return null;
            if (hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            {
                account.FailedLogins++;
                if (account.FailedLogins >= 5)
                {
                    account.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);
                    account.FailedLogins = 0;
                }
                await accounts.UpdateAsync(account);
                await units.Current!.SaveChangesAsync();
                return null;
            }
            account.FailedLogins = 0;
            account.LockedUntil = null;
            await accounts.UpdateAsync(account);
            return new(account.Id, account.TenantId!.Value, account.Email, account.Roles.Split(','), account.EmployeeId);
        }
    }
    [UnitOfWork(true)]
    public virtual async Task AcknowledgeAsync(Guid accountId)
    {
        if (!await privacy.AnyAsync(x => x.AccountId == accountId && x.Version == PrivacyVersion))
            await privacy.InsertAsync(new PrivacyAcceptance { TenantId = tenant.Id, AccountId = accountId, Version = PrivacyVersion });
    }
}
