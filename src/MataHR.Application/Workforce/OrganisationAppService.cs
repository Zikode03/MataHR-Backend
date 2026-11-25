using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
public class OrganisationAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<Office, Guid> offices, IRepository<Team, Guid> teams, IRepository<Job, Guid> jobs, IRepository<ChartCard, Guid> cards, IRepository<EmploymentChange, Guid> history, ReportingLineManager reporting) : WorkforceService(workers, audits), IOrganisationAppService
{
    [Authorize(WorkforcePermissions.OrganisationRead)]
    public virtual async Task<StructureDto> GetAsync()
    {
        Allow(Hr || Own != null);
        var ids = await Queries.ToListAsync((await Visible()).Select(x => x.Id));
        return new((await offices.GetListAsync()).Select(x => new BranchDto(x.Id, x.Name)).ToList(), (await teams.GetListAsync()).Select(x => new DepartmentDto(x.Id, x.BranchId, x.Name)).ToList(), (await jobs.GetListAsync()).Select(x => new PositionDto(x.Id, x.DepartmentId, x.Name)).ToList(), (await workers.GetListAsync(x => ids.Contains(x.Id))).Select(Dto).ToList(), (await cards.GetListAsync(x => ids.Contains(x.EmployeeId))).Select(x => new LayoutDto(x.EmployeeId, x.X, x.Y, x.Connector)).ToList());
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task<BranchDto> CreateBranchAsync(BranchInput input)
    {
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160, "Branch name required.");
        var row = await offices.InsertAsync(New(new Office { Name = input.Name.Trim() }));
        await Audit("Branch.Created", row.Id);
        return new(row.Id, row.Name);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task<DepartmentDto> CreateDepartmentAsync(DepartmentInput input)
    {
        await Get(offices, input.BranchId);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160, "Department name required.");
        var row = await teams.InsertAsync(New(new Team { BranchId = input.BranchId, Name = input.Name.Trim() }));
        await Audit("Department.Created", row.Id);
        return new(row.Id, row.BranchId, row.Name);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite)]
    public virtual async Task<PositionDto> CreatePositionAsync(PositionInput input)
    {
        await Get(teams, input.DepartmentId);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160, "Position name required.");
        var row = await jobs.InsertAsync(New(new Job { DepartmentId = input.DepartmentId, Name = input.Name.Trim() }));
        await Audit("Position.Created", row.Id);
        return new(row.Id, row.DepartmentId, row.Name);
    }

    [Authorize(WorkforcePermissions.OrganisationWrite), SerializableCommand]
    public virtual async Task<EmployeeDto> AssignAsync(AssignmentInput input)
    {
        return await InSerializableTransaction<EmployeeDto>(async () =>
        {
            var w = await Worker(input.EmployeeId);
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
        foreach (var i in input)
        {
            await Worker(i.EmployeeId);
            Require(double.IsFinite(i.X) && double.IsFinite(i.Y) && Math.Abs(i.X) <= 100000 && Math.Abs(i.Y) <= 100000 && i.Connector.Length <= 4000, "Invalid chart coordinates.");
            try
            {
                using var json = JsonDocument.Parse(i.Connector);
                Require(json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() <= 50, "Connector points must be an array.");
                foreach (var point in json.RootElement.EnumerateArray())
                {
                    Require(point.TryGetProperty("x", out var x) && point.TryGetProperty("y", out var y) && x.TryGetDouble(out var px) && y.TryGetDouble(out var py) && double.IsFinite(px) && double.IsFinite(py) && Math.Abs(px) <= 100000 && Math.Abs(py) <= 100000, "Invalid connector point.");
                }
            }
            catch (JsonException)
            {
                throw new Volo.Abp.UserFriendlyException("Invalid connector JSON.");
            }

            var row = await cards.FindAsync(x => x.EmployeeId == i.EmployeeId);
            if (row == null)
                row = await cards.InsertAsync(New(new ChartCard { EmployeeId = i.EmployeeId }));
            row.X = i.X;
            row.Y = i.Y;
            row.Connector = i.Connector;
            await cards.UpdateAsync(row);
        }

        await Audit("Organisation.LayoutSaved", TenantId);
    }
}
