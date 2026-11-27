using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using MataHR.Application;
using MataHR.Persistence;
using MataHR.HttpApi;
namespace MataHR;
[DependsOn(typeof(MataHrApplicationModule),typeof(MataHrEntityFrameworkCoreModule),typeof(MataHrHttpApiModule))]
public sealed class MataHrHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<MataHR.Contracts.IOrganisationRules, OrganisationRules>();
    }
}
