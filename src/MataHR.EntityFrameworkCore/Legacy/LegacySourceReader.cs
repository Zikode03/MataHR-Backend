using MataHR.Imports;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Volo.Abp.DependencyInjection;

namespace MataHR.Persistence;

// Keep the original table queries in the persistence adapter so the import service works with a typed snapshot.
[Volo.Abp.DependencyInjection.ExposeServices(typeof(ILegacySourceReader))]
public class LegacySourceReader(Volo.Abp.EntityFrameworkCore.IDbContextProvider<MataHrDbContext> provider, Microsoft.Extensions.Options.IOptions<LegacyImportOptions> options) : ILegacySourceReader, ITransientDependency
{
    public async Task<LegacySource> ReadAsync(Guid tenant, string kind)
    {
        if (kind is not ("Employees" or "Accounts"))
            throw new ArgumentException("Unknown import kind.");
        var db = await provider.GetDbContextAsync();
        var sourceTenant = options.Value.TargetTenantId == tenant && options.Value.SourceTenantId.HasValue ? options.Value.SourceTenantId.Value : tenant;
        var employees = new List<LegacyEmployee>();
        var accounts = new List<LegacyAccount>();
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close)
            await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenant";
            parameter.Value = sourceTenant;
            command.Parameters.Add(parameter);
            // Table and column names are constants, never supplied by a caller. Tenant is parameterised.
            command.CommandText = kind == "Employees" ? "IF OBJECT_ID('dbo.Employees','U') IS NOT NULL SELECT TOP (2001) Id,Number,Name,Email,Department,Position,ContractType,Started,Exited,ManagerId FROM dbo.Employees WHERE TenantId=@tenant ORDER BY Id" : "IF OBJECT_ID('dbo.Accounts','U') IS NOT NULL SELECT TOP (2001) Id,Email,Roles,EmployeeId,Active FROM dbo.Accounts WHERE TenantId=@tenant ORDER BY Id";
            await using var reader = await command.ExecuteReaderAsync();
            if (reader.FieldCount == 0)
                return new(employees, accounts, sourceTenant);
            while (await reader.ReadAsync())
            {
                string Text(int i) => reader.IsDBNull(i) ? "" : reader.GetString(i);
                if (kind == "Employees")
                    employees.Add(new(reader.GetGuid(0), Text(1), Text(2), Text(3), Text(4), Text(5), Text(6), DateOnly.FromDateTime(reader.GetDateTime(7)), reader.IsDBNull(8) ? null : DateOnly.FromDateTime(reader.GetDateTime(8)), reader.IsDBNull(9) ? null : reader.GetGuid(9)));
                else
                    accounts.Add(new(reader.GetGuid(0), Text(1), Text(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetBoolean(4)));
            }

            if (employees.Count > 2000 || accounts.Count > 2000)
                throw new Volo.Abp.UserFriendlyException("This import exceeds 2,000 records. Split and reconcile the legacy source before importing.");
            return new(employees, accounts, sourceTenant);
        }
        finally
        {
            if (close)
                await connection.CloseAsync();
        }
    }
}
