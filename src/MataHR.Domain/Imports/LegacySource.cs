namespace MataHR.Imports;
public record LegacyEmployee(Guid Id, string Number, string Name, string Email, string Department, string Position, string ContractType, DateOnly Started, DateOnly? Exited, Guid? ManagerId);
public record LegacyAccount(Guid Id, string Email, string Roles, Guid? EmployeeId, bool Active);
public record LegacySource(IReadOnlyList<LegacyEmployee> Employees, IReadOnlyList<LegacyAccount> Accounts, Guid SourceTenantId);
public interface ILegacySourceReader
{
    Task<LegacySource> ReadAsync(Guid tenant, string kind);
}

public class LegacyImportOptions
{
    public Guid? SourceTenantId { get; set; }
    public Guid? TargetTenantId { get; set; }
}
