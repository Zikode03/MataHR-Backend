using MataHR.Auditing;
using MataHR.Employees;
using MataHR.Common;
using MataHR.Employees.Contracts;
using MataHR.Organisation.Contracts;
using MataHR.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MataHR.Organisation;

// Reporting assignments change who manages an employee. Layout operations save presentation separately.
public class OrganisationAppService(IRepository<Employee, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<Branch, Guid> offices, IRepository<Department, Guid> teams, IRepository<Position, Guid> jobs, IRepository<ChartCard, Guid> cards, IRepository<EmploymentChange, Guid> history, ReportingLineManager reporting, IRepository<OrganisationCanvas, Guid> canvases) : WorkforceService(workers, audits), IOrganisationAppService
{
    [Authorize(WorkforcePermissions.OrganisationRead)]
    public virtual async Task<StructureDto> GetAsync()
    {
        Allow(Hr || Own != null);
        var directory = User.IsInRole("Employee") && !User.IsInRole("Manager") && !Hr ? await workers.GetQueryableAsync() : await Visible();
        if (!Hr)
            directory = directory.Where(x => x.Started <= Today && (x.Exited == null || x.Exited > Today));
        var ids = await Queries.ToListAsync(directory.Select(x => x.Id));
        var canvas = await canvases.FindAsync(x => x.TenantId == TenantId);
        return new((await offices.GetListAsync()).Select(x => new BranchDto(x.Id, x.Name)).ToList(), (await teams.GetListAsync()).Select(x => new DepartmentDto(x.Id, x.BranchId, x.Name)).ToList(), (await jobs.GetListAsync()).Select(x => new PositionDto(x.Id, x.DepartmentId, x.Name)).ToList(), (await workers.GetListAsync(x => ids.Contains(x.Id))).Select(x => new OrganisationPersonDto(x.Id, x.Number, x.Name, x.PositionId, x.ManagerId, x.IsActive(Today), x.ConcurrencyStamp)).ToList(), (await cards.GetListAsync(x => ids.Contains(x.EmployeeId))).Select(x => new LayoutDto(x.EmployeeId, x.X, x.Y, x.Connector)).ToList(), canvas == null ? null : new ChartPointDto(canvas.X, canvas.Y));
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task SaveCanvasAsync(ChartPointDto input)
    {
        var row = await canvases.FindAsync(x => x.TenantId == TenantId);
        if (row == null)
        {
            row = New(new OrganisationCanvas());
            row.Move(input.X, input.Y);
            await canvases.InsertAsync(row);
        }
        else
        {
            row.Move(input.X, input.Y);
            await canvases.UpdateAsync(row);
        }
        await Audit("Organisation.CanvasSaved", row.Id);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task<BranchDto> CreateBranchAsync(BranchInput input)
    {
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160, "Branch name required.");
        var row = await offices.InsertAsync(New(new Branch(input.Name)));
        await Audit("Branch.Created", row.Id);
        return new(row.Id, row.Name);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task<DepartmentDto> CreateDepartmentAsync(DepartmentInput input)
    {
        await Get(offices, input.BranchId);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160, "Department name required.");
        var row = await teams.InsertAsync(New(new Department(input.BranchId, input.Name)));
        await Audit("Department.Created", row.Id);
        return new(row.Id, row.BranchId, row.Name);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task<PositionDto> CreatePositionAsync(PositionInput input)
    {
        await Get(teams, input.DepartmentId);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160, "Position name required.");
        var row = await jobs.InsertAsync(New(new Position(input.DepartmentId, input.Name)));
        await Audit("Position.Created", row.Id);
        return new(row.Id, row.DepartmentId, row.Name);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite), SerializableCommand]
    public virtual async Task<EmployeeDto> AssignAsync(AssignmentInput input)
    {
        return await InSerializableTransaction<EmployeeDto>(async () =>
        {
            var w = await GetEmployee(input.EmployeeId);
            Stamp(w, input.ConcurrencyStamp);
            var before = w.Snapshot();
            await reporting.AssignAsync(w, input.PositionId, input.ManagerId, Today);
            await workers.UpdateAsync(w);
            await history.InsertAsync(new EmploymentChange(TenantId, w.Id, Today, "Organisation assignment changed", before, w.Snapshot()));
            await Audit("Organisation.Assigned", w.Id);
            await Flush();
            return Dto(w);
        });
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task SaveLayoutAsync(List<LayoutDto> input)
    {
        Require(input.Count <= 500 && input.Select(x => x.EmployeeId).Distinct().Count() == input.Count, "Invalid chart layout.");
        foreach (var card in input)
        {
            await GetEmployee(card.EmployeeId);
            var row = await cards.FindAsync(x => x.EmployeeId == card.EmployeeId);
            if (row == null)
                await cards.InsertAsync(New(new ChartCard(card.EmployeeId, card.X, card.Y, card.Connector)));
            else
            {
                row.Move(card.X, card.Y, card.Connector);
                await cards.UpdateAsync(row);
            }
        }

        await Audit("Organisation.LayoutSaved", TenantId);
    }
}
