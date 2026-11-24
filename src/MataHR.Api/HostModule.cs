using Volo.Abp.Modularity;
using MataHR.Application;
using MataHR.Persistence;
using MataHR.HttpApi;
namespace MataHR;
[DependsOn(typeof(MataHrApplicationModule),typeof(MataHrEntityFrameworkCoreModule),typeof(MataHrHttpApiModule))]
public sealed class MataHrHostModule:AbpModule {}
