using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Microsoft.Extensions.DependencyInjection;
using MataHR.Contracts;
using MataHR.Domain;
namespace MataHR.Application;
[DependsOn(typeof(MataHrDomainModule),typeof(MataHrApplicationContractsModule),typeof(AbpDddApplicationModule))]
public sealed class MataHrApplicationModule:AbpModule {
 public override void ConfigureServices(ServiceConfigurationContext context)=>context.Services.AddTransient<IOrganisationRules,OrganisationRules>();
}
public sealed class OrganisationRules:IOrganisationRules {
 public void ValidateAssignment(Guid employee,Guid? manager,IReadOnlyDictionary<Guid,Guid?> links)=>EmploymentRules.ValidateReporting(employee,manager,links);
}
