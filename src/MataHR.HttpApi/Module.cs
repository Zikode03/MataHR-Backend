using Volo.Abp.Modularity;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Contracts;
namespace MataHR.HttpApi;
[DependsOn(typeof(MataHrApplicationContractsModule),typeof(AbpAspNetCoreMvcModule))]
public sealed class MataHrHttpApiModule:AbpModule {}
