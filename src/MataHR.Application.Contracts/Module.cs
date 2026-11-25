using Volo.Abp.Modularity;
using Volo.Abp.Application;
using MataHR.Foundation;

namespace MataHR.Contracts;
[DependsOn(typeof(MataHrDomainSharedModule), typeof(AbpDddApplicationContractsModule))]
public sealed class MataHrApplicationContractsModule : AbpModule
{
}

public record OrganisationAssignmentInput(Guid EmployeeId, Guid PositionId, Guid? ManagerId);
public record BranchInput(string Name);
public record DepartmentInput(Guid BranchId, string Name);
public record PositionInput(Guid DepartmentId, string Name);
public record OrganisationNode(Guid EmployeeId, string EmployeeNumber, string Name, Guid PositionId, string Position, Guid DepartmentId, string Department, Guid BranchId, string Branch, Guid? ManagerId);
public record OrganisationSnapshot(IReadOnlyList<OrganisationNode> Assignments);
public interface IOrganisationRules
{
    void ValidateAssignment(Guid employee, Guid? manager, IReadOnlyDictionary<Guid, Guid?> links);
}
