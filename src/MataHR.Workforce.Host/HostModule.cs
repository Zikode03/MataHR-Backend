using Microsoft.AspNetCore.Identity;
using Volo.Abp.Auditing;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Autofac;
using MataHR.Application;
using MataHR.Persistence;
using MataHR.HttpApi;

namespace MataHR.Workforce.Host;
[DependsOn(typeof(AbpAutofacModule), typeof(Volo.Abp.Emailing.AbpEmailingModule), typeof(Volo.Abp.Identity.AspNetCore.AbpIdentityAspNetCoreModule), typeof(MataHrApplicationModule), typeof(MataHrEntityFrameworkCoreModule), typeof(MataHrHttpApiModule))]
public class WorkforceHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Retain ABP request/change auditing while redacting request payloads and sensitive entity fields.
        Configure<Volo.Abp.Auditing.AbpAuditingOptions>(o =>
        {
            o.IsEnabled = true;
            o.IsEnabledForAnonymousUsers = false;
            o.IsEnabledForGetRequests = false;
            o.Contributors.Add(new RedactedAuditContributor());
            o.EntityHistorySelectors.AddAllEntities();
        });
        Configure<Volo.Abp.PermissionManagement.PermissionManagementOptions>(o => o.SaveStaticPermissionsToDatabase = false);
        context.Services.TryAddTransient<MataHR.Workforce.Contracts.IAccountRecoveryDelivery, EmailRecoveryDelivery>();
        context.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        context.Services.AddIdentityCore<Volo.Abp.Identity.IdentityUser>().AddDefaultTokenProviders();
        Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
        Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(o =>
        {
            o.Password.RequiredLength = 14;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        });
        context.Services.Replace(ServiceDescriptor.Transient<Volo.Abp.AspNetCore.ExceptionHandling.IHttpExceptionStatusCodeFinder, WorkforceStatusCodeFinder>());
    }
}
