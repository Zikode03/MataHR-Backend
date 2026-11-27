using Volo.Abp.Modularity;
using Volo.Abp.Domain;
using MataHR.Foundation;

namespace MataHR.Domain;
// Domain rules stay independent of HTTP and EF Core so every entry point shares the same invariants.
[DependsOn(typeof(MataHrDomainSharedModule), typeof(AbpDddDomainModule), typeof(Volo.Abp.Identity.AbpIdentityDomainModule), typeof(Volo.Abp.PermissionManagement.Identity.AbpPermissionManagementDomainIdentityModule), typeof(Volo.Abp.PermissionManagement.AbpPermissionManagementDomainModule), typeof(Volo.Abp.AuditLogging.AbpAuditLoggingDomainModule))]
public sealed class MataHrDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(o =>
        {
            o.Password.RequiredLength = 6;
            o.Password.RequireUppercase = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireDigit = true;
            o.Password.RequireNonAlphanumeric = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        });
    }
}
