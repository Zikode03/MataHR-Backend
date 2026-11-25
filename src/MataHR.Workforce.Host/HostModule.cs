using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Autofac;
using MataHR.Application;
using MataHR.Persistence;
using MataHR.HttpApi;
namespace MataHR.Workforce.Host;

[DependsOn(typeof(AbpAutofacModule), typeof(MataHrApplicationModule), typeof(MataHrEntityFrameworkCoreModule), typeof(MataHrHttpApiModule))]
public class WorkforceHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Use explicit metadata audits; exclude automatic capture of credentials, salary and document payloads.
        Configure<Volo.Abp.Auditing.AbpAuditingOptions>(o => o.IsEnabled = false);
        context.Services.Replace(ServiceDescriptor.Transient<Volo.Abp.AspNetCore.ExceptionHandling.IHttpExceptionStatusCodeFinder, WorkforceStatusCodeFinder>());
    }
}
