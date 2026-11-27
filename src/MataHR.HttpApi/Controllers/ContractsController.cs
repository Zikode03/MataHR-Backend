using MataHR.Employees.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;

namespace MataHR.HttpApi;

// The employment service controls review and signing transitions before a contract record changes.
[Authorize, Route("api/contracts"), ApiController]
public class ContractsController(IEmployeesAppService service) : AbpController
{
    [HttpPost("")]
    public Task<ContractDto> Create(ContractInput input) => service.CreateContractAsync(input);
    [HttpPost("{id:guid}/transition")]
    public Task<ContractDto> Transition(Guid id, ContractDecision input) => service.TransitionContractAsync(id, input);
}
