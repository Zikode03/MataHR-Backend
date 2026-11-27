using MataHR.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace MataHR.Seeding;

public class WorkforcePermissionSeeder(IPermissionDataSeeder grants, IdentityRoleManager roles, ICurrentTenant tenant) : ITransientDependency
{
    public async Task SeedAsync(Guid tenantId)
    {
        using var tenantScope = tenant.Change(tenantId);
        foreach (var name in new[] { "Admin", "HR", "Manager", "Employee" })
        {
            // Defaults belong to new roles. Existing grants may have been edited by an administrator.
            if (await roles.FindByNameAsync(name) != null)
                continue;

            var result = await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), name, tenantId));
            if (!result.Succeeded)
                throw new Volo.Abp.UserFriendlyException("Could not create role.");

            await grants.SeedAsync(RolePermissionValueProvider.ProviderName, name,
                WorkforcePermissions.All.Where(permission => DefaultWorkforceRoleGrants.Allows([name], permission)), tenantId);
        }
    }
}
