using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using MataHR.Domain;

namespace MataHR.Persistence;
[DependsOn(typeof(MataHrDomainModule), typeof(AbpEntityFrameworkCoreSqlServerModule), typeof(Volo.Abp.Identity.EntityFrameworkCore.AbpIdentityEntityFrameworkCoreModule), typeof(Volo.Abp.PermissionManagement.EntityFrameworkCore.AbpPermissionManagementEntityFrameworkCoreModule), typeof(Volo.Abp.AuditLogging.EntityFrameworkCore.AbpAuditLoggingEntityFrameworkCoreModule))]
public sealed class MataHrEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<WorkforceDbContext>(o => o.AddDefaultRepositories(includeAllEntities: true));
        Configure<AbpDbContextOptions>(o => o.UseSqlServer());
    }
}
