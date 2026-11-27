using MataHR.Common.Contracts;
using Volo.Abp.Application.Services;
namespace MataHR.Accounts.Contracts;
public interface IAccountsAppService : IApplicationService
{
    Task<AdminOverviewDto> GetOverviewAsync();
    Task<List<EmployeeLinkDto>> GetEmployeeLinksAsync();
    Task<List<RoleGrantDto>> GetRoleGrantsAsync(string role);
    Task SetRolePermissionAsync(RolePermissionInput input);
    Task<AccountDto> UpdateAsync(Guid id, AccountUpdateInput input);
    Task<Page<AccountDto>> GetListAsync(PageQuery input);
    Task<AccountDto> CreateAsync(AccountInput input);
    Task<AccountDto> SetActiveAsync(Guid id, ActiveAccountInput input);
    Task<Page<AuditDto>> GetAuditAsync(PageQuery input);
}
