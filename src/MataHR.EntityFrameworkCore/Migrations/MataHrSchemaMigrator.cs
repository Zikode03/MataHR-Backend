using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
namespace MataHR.Persistence.Migrations;

// Schema changes are an explicit deployment step, not part of normal API startup.
public class MataHrSchemaMigrator(MataHrDbContext db) : ITransientDependency
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
        => await db.Database.MigrateAsync(cancellationToken);
}
