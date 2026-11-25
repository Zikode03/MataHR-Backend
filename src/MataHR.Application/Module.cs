using Volo.Abp.Modularity;
using Volo.Abp.Authorization.Permissions;
using MataHR.Workforce;
using Volo.Abp.Application;
using Microsoft.Extensions.DependencyInjection;
using MataHR.Contracts;
using MataHR.Domain;
namespace MataHR.Application;
[DependsOn(typeof(MataHrDomainModule),typeof(MataHrApplicationContractsModule),typeof(AbpDddApplicationModule))]
public sealed class MataHrApplicationModule:AbpModule {
 public override void ConfigureServices(ServiceConfigurationContext context){
 context.Services.AddTransient<IOrganisationRules,OrganisationRules>();
 context.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<WorkforceUser>,Microsoft.AspNetCore.Identity.PasswordHasher<WorkforceUser>>();
 Configure<Volo.Abp.Uow.AbpUnitOfWorkDefaultOptions>(o=>o.IsolationLevel=System.Data.IsolationLevel.Serializable);
 Configure<AbpPermissionOptions>(o=>o.ValueProviders.Add<WorkforceRolePermissionProvider>());
}
}
public sealed class OrganisationRules:IOrganisationRules {
 public void ValidateAssignment(Guid employee,Guid? manager,IReadOnlyDictionary<Guid,Guid?> links)=>EmploymentRules.ValidateReporting(employee,manager,links);
}
