using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Volo.Abp.Authorization;
using MataHR.Foundation;

namespace MataHR.Contracts;

// Client-facing contracts depend on shared types and permission definitions.
[DependsOn(typeof(MataHrDomainSharedModule), typeof(AbpDddApplicationContractsModule), typeof(AbpAuthorizationModule))]
public sealed class MataHrApplicationContractsModule : AbpModule
{
}
