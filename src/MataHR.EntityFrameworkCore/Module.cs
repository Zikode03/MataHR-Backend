using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using MataHR.Domain;
namespace MataHR.Persistence;
[DependsOn(typeof(MataHrDomainModule),typeof(AbpEntityFrameworkCoreSqliteModule))]
public sealed class MataHrEntityFrameworkCoreModule:AbpModule {}
