using MataHR.Common.Contracts;
using Volo.Abp.Application.Services;
namespace MataHR.Leave.Contracts;
public interface ILeaveAppService : IApplicationService
{
    Task<List<PolicyDto>> GetPoliciesAsync();
    Task<PolicyDto> CreatePolicyAsync(PolicyInput input);
    Task<List<HolidayInput>> GetHolidaysAsync(int year);
    Task AddHolidayAsync(HolidayInput input);
    Task SetAllocationAsync(AllocationInput input);
    Task<List<BalanceDto>> GetBalancesAsync(Guid employeeId, int year);
    Task<Page<LeaveDto>> GetListAsync(PageQuery input);
    Task<LeaveDto> SubmitAsync(LeaveInput input, Guid? employeeId = null);
    Task<LeaveDto> ReviewAsync(Guid id, DecisionInput input);
    Task<LeaveDto> WithdrawAsync(Guid id, string concurrencyStamp);
    Task<LeaveDto> RequestCancellationAsync(Guid id, string concurrencyStamp);
}
