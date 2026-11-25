using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;

namespace MataHR.HttpApi;
[Authorize, Route("api/admin"), ApiController]
public class AccountsController(IAccountsAppService service) : AbpController
{
    [HttpGet("employee-links")]
    public Task<List<EmployeeLinkDto>> EmployeeLinks() => service.GetEmployeeLinksAsync();
    [HttpGet("role-permissions")]
    public Task<List<RoleGrantDto>> Grants(string role) => service.GetRoleGrantsAsync(role);
    [HttpGet("accounts")]
    public Task<Page<AccountDto>> Accounts([FromQuery] PageQuery input) => service.GetListAsync(input);
    [HttpPost("accounts")]
    public Task<AccountDto> Create(AccountInput input) => service.CreateAsync(input);
    [HttpPut("accounts/{id:guid}/active")]
    public Task<AccountDto> Active(Guid id, ActiveAccountInput input) => service.SetActiveAsync(id, input);
    [HttpGet("audit")]
    public Task<Page<AuditDto>> Audit([FromQuery] PageQuery input) => service.GetAuditAsync(input);
    [HttpPut("accounts/{id:guid}")]
    public Task<AccountDto> Update(Guid id, AccountUpdateInput input) => service.UpdateAsync(id, input);
    [HttpPut("role-permissions")]
    public Task SetPermission(RolePermissionInput input) => service.SetRolePermissionAsync(input);
}
