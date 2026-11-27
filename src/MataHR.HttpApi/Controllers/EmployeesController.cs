using MataHR.Common.Contracts;
using MataHR.Employees.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;

namespace MataHR.HttpApi;

// The application service decides which employee records the caller can read or change.
[Authorize, Route("api/employees"), ApiController]
public class EmployeesController(IEmployeesAppService service) : AbpController
{
    [HttpGet("")]
    public Task<Page<EmployeeDto>> Get([FromQuery] PageQuery input) => service.GetListAsync(input);
    [HttpGet("{id:guid}")]
    public Task<EmployeeDto> Get(Guid id) => service.GetAsync(id);
    [HttpPost("")]
    public Task<EmployeeDto> Create(EmployeeInput input) => service.CreateAsync(input);
    [HttpPut("{id:guid}")]
    public Task<EmployeeDto> Update(Guid id, EmployeeInput input) => service.UpdateAsync(id, input);
    [HttpGet("{id:guid}/history")]
    public Task<List<EmployeeHistoryDto>> History(Guid id) => service.GetHistoryAsync(id);
    [HttpGet("{id:guid}/contracts")]
    public Task<List<ContractDto>> Contracts(Guid id) => service.GetContractsAsync(id);
}
