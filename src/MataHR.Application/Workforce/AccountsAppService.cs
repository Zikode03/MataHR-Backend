using Volo.Abp.PermissionManagement;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
public class AccountsAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<Volo.Abp.Identity.IdentityUser, Guid> accounts, Volo.Abp.Identity.IdentityUserManager users, Volo.Abp.PermissionManagement.IPermissionManager grants) : WorkforceService(workers, audits), IAccountsAppService
{
    [Authorize(WorkforcePermissions.Accounts)]
    public virtual async Task<Page<AccountDto>> GetListAsync(PageQuery input)
    {
        PageBounds(input);
        var q = await accounts.GetQueryableAsync();
        return new(await Queries.LongCountAsync(q), await Dtos(await Queries.ToListAsync(q.OrderBy(x => x.Email).Skip(input.SkipCount).Take(input.MaxResultCount))));
    }

    [Authorize(WorkforcePermissions.Accounts)]
    public virtual async Task<AccountDto> CreateAsync(AccountInput input)
    {
        Require(new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.Email) && input.Email.Length <= 254 && input.Password.Length >= 14 && input.Roles.Length > 0 && input.Roles.All(x => new[] { "Admin", "HR", "Manager", "Employee" }.Contains(x)), "Valid email, assigned roles and 14-character account password required.");
        if (input.EmployeeId.HasValue)
            await Worker(input.EmployeeId.Value);
        Require(!input.Roles.Any(x => x is "Employee" or "Manager") || input.EmployeeId.HasValue, "Employee and manager accounts must be linked to an employee.");
        var email = input.Email.Trim().ToLowerInvariant();
        Require(!await accounts.AnyAsync(x => x.Email == email), "Account already exists.");
        var row = new Volo.Abp.Identity.IdentityUser(Guid.NewGuid(), email, email, TenantId);
        row.SetProperty("EmployeeId", input.EmployeeId);
        Check(await users.CreateAsync(row, input.Password));
        Check(await users.SetRolesAsync(row, input.Roles.Distinct()));
        await Audit("Account.Created", row.Id);
        return await Dto(row);
    }

    [Authorize(WorkforcePermissions.Accounts), UnitOfWork(true)]
    public virtual async Task<AccountDto> SetActiveAsync(Guid id, ActiveAccountInput input)
    {
        var row = await accounts.FindAsync(id);
        if (row == null || row.TenantId != TenantId)
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Volo.Abp.Identity.IdentityUser), id);
        Allow(row.Id != Actor);
        row.SetIsActive(input.Active);
        Check(await users.UpdateAsync(row));
        Check(await users.UpdateSecurityStampAsync(row));
        await Audit(input.Active ? "Account.Activated" : "Account.Deactivated", id);
        return await Dto(row);
    }

    [Authorize(WorkforcePermissions.Audit)]
    public virtual async Task<Page<AuditDto>> GetAuditAsync(PageQuery input)
    {
        PageBounds(input);
        var q = await audits.GetQueryableAsync();
        return new(await Queries.LongCountAsync(q), (await Queries.ToListAsync(q.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount))).Select(x => new AuditDto(x.Id, x.ActorId, x.Action, x.TargetId, x.CreationTime, Hr ? x.Note : null)).ToList());
    }

    [Authorize(WorkforcePermissions.Accounts), UnitOfWork(true)]
    public virtual async Task<AccountDto> UpdateAsync(Guid id, AccountUpdateInput input)
    {
        var row = await accounts.FindAsync(id);
        if (row == null || row.TenantId != TenantId)
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Volo.Abp.Identity.IdentityUser), id);
        Require(row.ConcurrencyStamp == input.ConcurrencyStamp, "Account changed. Refresh and retry.");
        Allow(row.Id != Actor);
        Require(input.Roles.Length > 0 && input.Roles.All(x => new[] { "Admin", "HR", "Manager", "Employee" }.Contains(x)), "Valid assigned roles required.");
        if (input.EmployeeId.HasValue)
            await Worker(input.EmployeeId.Value);
        Require(!input.Roles.Any(x => x is "Employee" or "Manager") || input.EmployeeId.HasValue, "Employee or manager must be linked to an employee.");
        Check(await users.SetRolesAsync(row, input.Roles.Distinct()));
        row.SetProperty("EmployeeId", input.EmployeeId);
        row.SetIsActive(input.Active);
        Check(await users.UpdateAsync(row));
        Check(await users.UpdateSecurityStampAsync(row));
        await Audit("Account.AssignmentChanged", id);
        await Flush();
        return await Dto(row);
    }

    [Authorize(WorkforcePermissions.Accounts)]
    public virtual async Task SetRolePermissionAsync(RolePermissionInput input)
    {
        Require(new[] { "Admin", "HR", "Manager", "Employee" }.Contains(input.Role) && WorkforcePermissions.All.Contains(input.Permission), "Unknown role or permission.");
        Require(!(input.Role == "Admin" && input.Permission == WorkforcePermissions.Accounts && !input.Granted), "The system administrator management grant cannot be removed through this endpoint.");
        await grants.SetAsync(input.Permission, Volo.Abp.Authorization.Permissions.RolePermissionValueProvider.ProviderName, input.Role, input.Granted);
        await Audit("Role.PermissionChanged", TenantId, input.Role + ":" + input.Permission + ":" + input.Granted);
    }

    [Authorize(WorkforcePermissions.Accounts)]
    public virtual async Task<List<EmployeeLinkDto>> GetEmployeeLinksAsync() => await Queries.ToListAsync((await workers.GetQueryableAsync()).OrderBy(x => x.Number).Select(x => new EmployeeLinkDto(x.Id, x.Number, x.Name)));
    [Authorize(WorkforcePermissions.Accounts)]
    public virtual async Task<List<RoleGrantDto>> GetRoleGrantsAsync(string role)
    {
        Require(new[] { "Admin", "HR", "Manager", "Employee" }.Contains(role), "Unknown role.");
        var result = new List<RoleGrantDto>();
        foreach (var permission in WorkforcePermissions.All)
        {
            var grant = await grants.GetAsync(permission, "R", role);
            result.Add(new(permission, grant.IsGranted));
        }

        return result;
    }

    async Task<AccountDto> Dto(Volo.Abp.Identity.IdentityUser x) => new(x.Id, x.Email, (await users.GetRolesAsync(x)).ToArray(), x.GetProperty<Guid?>("EmployeeId"), x.IsActive, x.ConcurrencyStamp);
    async Task<List<AccountDto>> Dtos(List<Volo.Abp.Identity.IdentityUser> rows)
    {
        var result = new List<AccountDto>();
        foreach (var row in rows)
            result.Add(await Dto(row));
        return result;
    }

    static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new Volo.Abp.UserFriendlyException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
