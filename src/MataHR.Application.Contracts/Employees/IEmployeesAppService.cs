using MataHR.Common.Contracts;
using Volo.Abp.Application.Services;
namespace MataHR.Employees.Contracts;
public interface IEmployeesAppService : IApplicationService
{
    Task<Page<EmployeeDto>> GetListAsync(PageQuery input);
    Task<EmployeeDto> GetAsync(Guid id);
    Task<EmployeeDto> CreateAsync(EmployeeInput input);
    Task<EmployeeDto> UpdateAsync(Guid id, EmployeeInput input);
    Task<List<EmployeeHistoryDto>> GetHistoryAsync(Guid id);
    Task<List<ContractDto>> GetContractsAsync(Guid id);
    Task<ContractDto> CreateContractAsync(ContractInput input);
    Task<ContractDto> TransitionContractAsync(Guid id, ContractDecision input);
}
