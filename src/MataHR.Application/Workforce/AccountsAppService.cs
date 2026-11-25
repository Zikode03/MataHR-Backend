using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;
namespace MataHR.Workforce;

public class AccountsAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<WorkforceUser, Guid> accounts, IPasswordHasher<WorkforceUser> hasher) : WorkforceService(workers, audits), IAccountsAppService
{
    [Authorize(WorkforcePermissions.Accounts)]
    public virtual async Task<Page<AccountDto>> GetListAsync(PageQuery input)
    {
        PageBounds(input);
        var q = await accounts.GetQueryableAsync();
        return new(await Queries.LongCountAsync(q), (await Queries.ToListAsync(q.OrderBy(x => x.Email).Skip(input.SkipCount).Take(input.MaxResultCount))).Select(Dto).ToList());
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
        var row = New(new WorkforceUser { Email = email, Roles = string.Join(',', input.Roles.Distinct()), EmployeeId = input.EmployeeId });
        row.PasswordHash = hasher.HashPassword(row, input.Password);
        await accounts.InsertAsync(row);
        await Audit("Account.Created", row.Id);
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.Accounts), UnitOfWork(true)]
    public virtual async Task<AccountDto> SetActiveAsync(Guid id, ActiveAccountInput input)
    {
        var row = await Get(accounts, id);
        Allow(row.Id != Actor);
        row.Active = input.Active;
        await accounts.UpdateAsync(row);
        await Audit(input.Active ? "Account.Activated" : "Account.Deactivated", id);
        return Dto(row);
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
        var row = await Get(accounts, id);
        Stamp(row, input.ConcurrencyStamp);
        Allow(row.Id != Actor);
        Require(input.Roles.Length > 0 && input.Roles.All(x => new[] { "Admin", "HR", "Manager", "Employee" }.Contains(x)), "Valid assigned roles required.");
        if (input.EmployeeId.HasValue)
            await Worker(input.EmployeeId.Value);
        Require(!input.Roles.Any(x => x is "Employee" or "Manager") || input.EmployeeId.HasValue, "Employee or manager must be linked to an employee.");
        row.Roles = string.Join(',', input.Roles.Distinct());
        row.EmployeeId = input.EmployeeId;
        row.Active = input.Active;
        await accounts.UpdateAsync(row);
        await Audit("Account.AssignmentChanged", id);
        await Flush();
        return Dto(row);
    }
    static AccountDto Dto(WorkforceUser x) => new(x.Id, x.Email, x.Roles.Split(','), x.EmployeeId, x.Active, x.ConcurrencyStamp);
}
