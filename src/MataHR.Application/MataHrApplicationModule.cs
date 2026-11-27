using Volo.Abp.Modularity;
using Volo.Abp.Application;
using MataHR.Contracts;
using MataHR.Domain;

namespace MataHR.Application;
// Compose application services here; database configuration belongs to the persistence module.
[DependsOn(typeof(MataHrDomainModule), typeof(MataHrApplicationContractsModule), typeof(AbpDddApplicationModule), typeof(Volo.Abp.Identity.AbpIdentityApplicationModule), typeof(Volo.Abp.PermissionManagement.AbpPermissionManagementApplicationModule))]
public sealed class MataHrApplicationModule : AbpModule
{
}
