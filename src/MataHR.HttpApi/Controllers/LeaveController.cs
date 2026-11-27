using MataHR.Common.Contracts;
using MataHR.Leave.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;

namespace MataHR.HttpApi;

// The service owns balance reservations and review stages so every endpoint follows the same rules.
[Authorize, Route("api/leave"), ApiController]
public class LeaveController(ILeaveAppService service) : AbpController
{
    [HttpGet("policies")]
    public Task<List<PolicyDto>> Policies() => service.GetPoliciesAsync();
    [HttpPost("policies")]
    public Task<PolicyDto> Policy(PolicyInput input) => service.CreatePolicyAsync(input);
    [HttpGet("holidays")]
    public Task<List<HolidayInput>> Holidays(int year) => service.GetHolidaysAsync(year);
    [HttpPost("holidays")]
    public Task Holiday(HolidayInput input) => service.AddHolidayAsync(input);
    [HttpPut("entitlements")]
    public Task Allocate(AllocationInput input) => service.SetAllocationAsync(input);
    [HttpGet("balances/{employeeId:guid}")]
    public Task<List<BalanceDto>> Balances(Guid employeeId, int year) => service.GetBalancesAsync(employeeId, year);
    [HttpGet("requests")]
    public Task<Page<LeaveDto>> Requests([FromQuery] PageQuery input) => service.GetListAsync(input);
    [HttpPost("requests")]
    public Task<LeaveDto> Submit(LeaveInput input, [FromQuery] Guid? employeeId = null) => service.SubmitAsync(input, employeeId);
    [HttpPost("requests/{id:guid}/review")]
    public Task<LeaveDto> Review(Guid id, DecisionInput input) => service.ReviewAsync(id, input);
    [HttpPost("requests/{id:guid}/withdraw")]
    public Task<LeaveDto> Withdraw(Guid id, [FromBody] string concurrencyStamp) => service.WithdrawAsync(id, concurrencyStamp);
    [HttpPost("requests/{id:guid}/cancel")]
    public Task<LeaveDto> Cancel(Guid id, [FromBody] string concurrencyStamp) => service.RequestCancellationAsync(id, concurrencyStamp);
}
