using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
public class EmployeesAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<EmploymentChange, Guid> history, IRepository<WorkContract, Guid> contracts, IRepository<SecureDocument, Guid> documents, ReportingLineManager reporting) : WorkforceService(workers, audits), IEmployeesAppService
{
    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<Page<EmployeeDto>> GetListAsync(PageQuery input)
    {
        PageBounds(input);
        var q = await Visible();
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim();
            q = q.Where(x => x.Name.Contains(search) || x.Number.Contains(search));
        }

        return new(await Queries.LongCountAsync(q), (await Queries.ToListAsync(q.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount))).Select(Dto).ToList());
    }

    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<EmployeeDto> GetAsync(Guid id)
    {
        await View(id);
        return Dto(await Worker(id));
    }

    [Authorize(WorkforcePermissions.PeopleWrite), SerializableCommand]
    public virtual async Task<EmployeeDto> CreateAsync(EmployeeInput input) => await InSerializableTransaction(() => Save(input, null));
    [Authorize(WorkforcePermissions.PeopleWrite), SerializableCommand]
    public virtual async Task<EmployeeDto> UpdateAsync(Guid id, EmployeeInput input) => await InSerializableTransaction(() => Save(input, id));
    async Task<EmployeeDto> Save(EmployeeInput i, Guid? id)
    {
        Require(!string.IsNullOrWhiteSpace(i.Number) && i.Number.Length <= 64 && !string.IsNullOrWhiteSpace(i.Name) && i.Name.Length <= 160 && i.Email.Length <= 254 && i.Started != default && (i.Exited == null || i.Exited >= i.Started), "Invalid employee fields.");
        var row = id.HasValue ? await Worker(id.Value) : new Worker(TenantId, i.Number, i.Name, i.Email, i.Started, i.Exited, i.ContractType);
        if (id.HasValue)
            Stamp(row, i.ConcurrencyStamp);
        var before = id.HasValue ? row.Snapshot() : "";
        Require(!await workers.AnyAsync(x => x.TenantId == TenantId && x.Id != row.Id && x.Number == i.Number.Trim()), "Employee number already exists.");
        row.ChangeDetails(i.Number, i.Name, i.Email, new EmploymentPeriod(i.Started, i.Exited), i.ContractType);
        await reporting.AssignAsync(row, i.PositionId, i.ManagerId, Today);
        if (id == null)
            await workers.InsertAsync(row);
        else
            await workers.UpdateAsync(row);
        await history.InsertAsync(new EmploymentChange(TenantId, row.Id, Today, id == null ? "Employee created" : "Employment details updated", before, row.Snapshot()));
        await Audit(id == null ? "Employee.Created" : "Employee.Updated", row.Id);
        await Flush();
        return Dto(row);
    }

    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<List<EmployeeHistoryDto>> GetHistoryAsync(Guid id)
    {
        await View(id, true);
        return (await history.GetListAsync(x => x.EmployeeId == id)).OrderByDescending(x => x.EffectiveDate).Select(x => new EmployeeHistoryDto(x.Id, x.EffectiveDate, x.Description, x.BeforeStateJson, x.AfterStateJson)).ToList();
    }

    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<List<ContractDto>> GetContractsAsync(Guid id)
    {
        await View(id, true);
        return (await contracts.GetListAsync(x => x.EmployeeId == id)).Select(ContractDto).ToList();
    }

    [Authorize(WorkforcePermissions.PeopleWrite)]
    public virtual async Task<ContractDto> CreateContractAsync(ContractInput input)
    {
        await Worker(input.EmployeeId);
        Require(!string.IsNullOrWhiteSpace(input.Reference) && input.Reference.Length <= 100 && input.Start != default && (input.End == null || input.End >= input.Start), "Invalid contract.");
        var row = new WorkContract(TenantId, input.EmployeeId, input.Reference, input.Start, input.End);
        await contracts.InsertAsync(row);
        await Audit("Contract.Created", row.Id);
        await Flush();
        return ContractDto(row);
    }

    [Authorize(WorkforcePermissions.PeopleWrite), SerializableCommand]
    public virtual async Task<ContractDto> TransitionContractAsync(Guid id, ContractDecision input)
    {
        var row = await Get(contracts, id);
        Stamp(row, input.ConcurrencyStamp);
        Require(Enum.TryParse<ContractState>(input.Status, out var next), "Invalid contract status.");
        SecureDocument? evidence = null;
        if (input.DocumentId.HasValue)
            evidence = await Get(documents, input.DocumentId.Value);
        row.Transition(next, Actor, input.Note, evidence);
        await contracts.UpdateAsync(row);
        await Audit("Contract." + input.Status, row.Id, input.Note);
        await Flush();
        return ContractDto(row);
    }

    static ContractDto ContractDto(WorkContract x) => new(x.Id, x.EmployeeId, x.Reference, x.Start, x.End, x.Status, x.Version, x.DocumentId, x.SignedDocumentId, x.ConcurrencyStamp);
}
