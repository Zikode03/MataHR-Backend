using MataHR.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using MataHR.Application;
using MataHR.Persistence;
namespace MataHR.DbMigrator;
[DependsOn(typeof(AbpAutofacModule), typeof(MataHrApplicationModule), typeof(MataHrEntityFrameworkCoreModule))]
public class MataHrDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<WorkforceBootstrapOptions>(context.Services.GetConfiguration().GetSection("Bootstrap"));
        // Use our role policy instead of ABP's generic administrator seed, which would grant broader access.
        PostConfigure<AbpDataSeedOptions>(options =>
        {
            options.Contributors.Clear();
            options.Contributors.Add<WorkforceDataSeedContributor>();
        });
    }
}
