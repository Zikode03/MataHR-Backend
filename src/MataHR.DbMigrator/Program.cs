using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Uow;
using MataHR.DbMigrator;
using MataHR.Persistence;
using MataHR.Persistence.Migrations;

AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
var migrateOnly = args.Contains("--migrate-only");
var configuration = new ConfigurationBuilder().AddUserSecrets<MataHrDbMigratorModule>(optional: true)
    .AddEnvironmentVariables().AddCommandLine(args.Where(a => a != "--migrate-only").ToArray()).Build();
if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
    throw new InvalidOperationException("Configure ConnectionStrings__Default before running the migrator.");
using var application = await AbpApplicationFactory.CreateAsync<MataHrDbMigratorModule>(options =>
{
    options.UseAutofac();
    options.Services.ReplaceConfiguration(configuration);
});
await application.InitializeAsync();
try
{
    using var scope = application.ServiceProvider.CreateScope();
    // Create the schema first because the seeder writes to the newly created Identity and permission tables.
    await scope.ServiceProvider.GetRequiredService<MataHrSchemaMigrator>().MigrateAsync();
    if (!migrateOnly)
    {
        if (!Guid.TryParse(configuration["Bootstrap:TenantId"], out var tenant))
            throw new InvalidOperationException("Configure Bootstrap:TenantId before seeding.");
        using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.Serializable);
        await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync(new DataSeedContext(tenant));
        await unit.CompleteAsync();
    }
    Console.WriteLine("MataHR database setup completed.");
}
finally { await application.ShutdownAsync(); }
