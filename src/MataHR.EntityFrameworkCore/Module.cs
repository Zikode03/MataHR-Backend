using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using MataHR.Domain;
namespace MataHR.Persistence;
[DependsOn(typeof(MataHrDomainModule),typeof(AbpEntityFrameworkCoreSqlServerModule))]
public sealed class MataHrEntityFrameworkCoreModule:AbpModule {}
