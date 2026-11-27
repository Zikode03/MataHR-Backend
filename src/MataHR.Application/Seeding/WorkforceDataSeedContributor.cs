using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
namespace MataHR.Seeding;

// Reruns create missing setup only: no password reset, reactivation or wider permission grants.
public class WorkforceDataSeedContributor(WorkforcePermissionSeeder roles, IdentityUserManager users,
    IRepository<IdentityUser, Guid> accounts, ICurrentTenant tenancy, IOptions<WorkforceBootstrapOptions> options)
    : IDataSeedContributor, ITransientDependency
{
    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        var tenant = context.TenantId ?? throw new InvalidOperationException("A tenant ID is required for MataHR seeding.");
        using (tenancy.Change(tenant))
        {
            await roles.SeedAsync(tenant);
            var settings = options.Value;
            if (string.IsNullOrWhiteSpace(settings.Email) && string.IsNullOrWhiteSpace(settings.Password)) return;
            if (await accounts.AnyAsync()) return;
            if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
                throw new InvalidOperationException("Supply Bootstrap:Email and Bootstrap:Password to seed the first administrator.");
            var email = settings.Email.Trim().ToLowerInvariant();
            var user = new IdentityUser(Guid.NewGuid(), email, email, tenant);
            Check(await users.CreateAsync(user, settings.Password));
            Check(await users.AddToRoleAsync(user, "Admin"));
        }
    }
    static void Check(Microsoft.AspNetCore.Identity.IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Administrator seeding failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
