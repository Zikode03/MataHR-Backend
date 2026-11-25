using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;
namespace MataHR.Workforce;

public class EmployeesAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<Job, Guid> jobs, IRepository<EmploymentChange, Guid> history, IRepository<WorkContract, Guid> contracts, IRepository<SecureDocument, Guid> documents) : WorkforceService(workers, audits), IEmployeesAppService
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
    [Authorize(WorkforcePermissions.PeopleWrite), UnitOfWork(true)]
    public virtual async Task<EmployeeDto> CreateAsync(EmployeeInput input) => await Save(input, null);
    [Authorize(WorkforcePermissions.PeopleWrite), UnitOfWork(true)]
    public virtual async Task<EmployeeDto> UpdateAsync(Guid id, EmployeeInput input) => await Save(input, id);
    async Task<EmployeeDto> Save(EmployeeInput i, Guid? id)
    {
        Require(!string.IsNullOrWhiteSpace(i.Number) && i.Number.Length <= 64 && !string.IsNullOrWhiteSpace(i.Name) && i.Name.Length <= 160 && i.Email.Length <= 254 && i.Started != default && (i.Exited == null || i.Exited >= i.Started), "Invalid employee fields.");
        var row = id.HasValue ? await Worker(id.Value) : New(new Worker());
        if (id.HasValue)
            Stamp(row, i.ConcurrencyStamp);
        var links = (await Queries.ToListAsync((await workers.GetQueryableAsync()).Where(x => x.TenantId == TenantId))).ToDictionary(x => x.Id, x => x.ManagerId);
        links[row.Id] = row.ManagerId;
        try
        {
            MataHR.Domain.EmploymentRules.ValidateReporting(row.Id, i.ManagerId, links);
        }
        catch (MataHR.Foundation.DomainRuleException e) { throw new Volo.Abp.UserFriendlyException(e.Message); }
        if (i.ManagerId.HasValue)
            Require(Active(await Worker(i.ManagerId.Value), Today), "Active manager required.");
        if (i.PositionId.HasValue)
            await Get(jobs, i.PositionId.Value);
        Require(!await workers.AnyAsync(x => x.TenantId == TenantId && x.Id != row.Id && x.Number == i.Number.Trim()), "Employee number already exists.");
        row.Number = i.Number.Trim();
        row.Name = i.Name.Trim();
        row.Email = i.Email.Trim();
        row.Started = i.Started;
        row.Exited = i.Exited;
        row.ContractType = i.ContractType;
        row.ManagerId = i.ManagerId;
        row.PositionId = i.PositionId;
        if (id == null)
            await workers.InsertAsync(row);
        else
            await workers.UpdateAsync(row);
        await history.InsertAsync(New(new EmploymentChange { EmployeeId = row.Id, EffectiveDate = Today, Description = id == null ? "Employee created" : "Employment details updated" }));
        await Audit(id == null ? "Employee.Created" : "Employee.Updated", row.Id);
        await Flush();
        return Dto(row);
    }
    [Authorize(WorkforcePermissions.PeopleRead)]
    public virtual async Task<List<EmployeeHistoryDto>> GetHistoryAsync(Guid id)
    {
        await View(id, true);
        return (await history.GetListAsync(x => x.EmployeeId == id)).OrderByDescending(x => x.EffectiveDate).Select(x => new EmployeeHistoryDto(x.Id, x.EffectiveDate, x.Description)).ToList();
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
        var row = New(new WorkContract { EmployeeId = input.EmployeeId, Reference = input.Reference.Trim(), Start = input.Start, End = input.End });
        await contracts.InsertAsync(row);
        await Audit("Contract.Created", row.Id);
        await Flush();
        return ContractDto(row);
    }
    [Authorize(WorkforcePermissions.PeopleWrite), UnitOfWork(true)]
    public virtual async Task<ContractDto> TransitionContractAsync(Guid id, ContractDecision input)
    {
        var row = await Get(contracts, id);
        Stamp(row, input.ConcurrencyStamp);
        var actor = Actor;
        Require(Enum.TryParse<MataHR.Domain.ContractStage>(row.Status, out var current) && Enum.TryParse<MataHR.Domain.ContractStage>(input.Status, out _), "Invalid contract status.");
        var next = Enum.Parse<MataHR.Domain.ContractStage>(input.Status);
        if (input.DocumentId.HasValue)
        {
            var doc = await Get(documents, input.DocumentId.Value);
            Require(doc.EmployeeId == row.EmployeeId && doc.Kind == "Contract" && doc.VerifiedBy != null, "Verified contract document belonging to this employee required.");
            if (next == MataHR.Domain.ContractStage.Signed)
                row.SignedDocumentId = doc.Id;
            else
            {
                Require(current == MataHR.Domain.ContractStage.Draft, "Reviewed contract document is locked.");
                row.DocumentId = doc.Id;
            }
        }
        try
        {
            MataHR.Domain.ContractRules.ValidateTransition(current, next, row.DocumentId.HasValue, row.SignedDocumentId.HasValue, input.Note, actor, row.SubmittedBy);
        }
        catch (MataHR.Foundation.DomainRuleException e) { throw new Volo.Abp.UserFriendlyException(e.Message); }
        if (next == MataHR.Domain.ContractStage.InReview)
            row.SubmittedBy = actor;
        if (next == MataHR.Domain.ContractStage.Approved)
            row.ApprovedBy = actor;
        row.Status = input.Status;
        await contracts.UpdateAsync(row);
        await Audit("Contract." + input.Status, row.Id, input.Note);
        await Flush();
        return ContractDto(row);
    }
    static ContractDto ContractDto(WorkContract x) => new(x.Id, x.EmployeeId, x.Reference, x.Start, x.End, x.Status, x.Version, x.DocumentId, x.SignedDocumentId, x.ConcurrencyStamp);
}
