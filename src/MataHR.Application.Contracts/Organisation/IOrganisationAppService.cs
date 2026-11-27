using MataHR.Employees.Contracts;
using Volo.Abp.Application.Services;
namespace MataHR.Organisation.Contracts;

// Keep reporting assignments and chart placement separate in the contract so a visual move cannot imply a manager change.
public interface IOrganisationAppService : IApplicationService
{
    Task SaveCanvasAsync(ChartPointDto input);
    Task<StructureDto> GetAsync();
    Task<BranchDto> CreateBranchAsync(BranchInput input);
    Task<DepartmentDto> CreateDepartmentAsync(DepartmentInput input);
    Task<PositionDto> CreatePositionAsync(PositionInput input);
    Task<EmployeeDto> AssignAsync(AssignmentInput input);
    Task SaveLayoutAsync(List<LayoutDto> input);
}
