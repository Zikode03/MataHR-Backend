using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using MataHR.Domain;
namespace MataHR.Persistence;
[DependsOn(typeof(MataHrDomainModule),typeof(AbpEntityFrameworkCoreSqlServerModule))]
public sealed class MataHrEntityFrameworkCoreModule:AbpModule {
 public override void ConfigureServices(ServiceConfigurationContext context){
 context.Services.AddAbpDbContext<WorkforceDbContext>(o=>o.AddDefaultRepositories(includeAllEntities:true));
 Configure<AbpDbContextOptions>(o=>o.UseSqlServer());
 }
}
