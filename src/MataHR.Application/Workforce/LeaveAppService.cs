using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
public class LeaveAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<AbsencePolicy, Guid> policies, IRepository<LeaveAllocation, Guid> allocations, IRepository<Absence, Guid> requests, IRepository<PublicHoliday, Guid> holidays, IRepository<SecureDocument, Guid> documents, IRepository<WorkSession, Guid> sessions, CalendarService calendar) : WorkforceService(workers, audits), ILeaveAppService
{
    [Authorize(WorkforcePermissions.LeaveOwn)]
    public virtual async Task<List<PolicyDto>> GetPoliciesAsync() => (await policies.GetListAsync()).Select(Dto).ToList();
    [Authorize(WorkforcePermissions.LeaveConfigure)]
    public virtual async Task<PolicyDto> CreatePolicyAsync(PolicyInput input) => await InSerializableTransaction<PolicyDto>(async () =>
    {
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160 && input.AnnualDays >= 0 && input.CarryLimit >= 0 && input.NoticeDays >= 0 && input.EvidenceAfter >= 0, "Invalid policy.");
        var versions = await policies.GetListAsync(x => x.Name == input.Name.Trim());
        foreach (var old in versions)
        {
            old.Retire();
            await policies.UpdateAsync(old);
        }

        var row = await policies.InsertAsync(new AbsencePolicy(TenantId, input.Name, versions.Count + 1, input.Paid, input.Limited, input.AnnualDays, input.MonthlyAccrual, input.CarryLimit, input.NoticeDays, input.EvidenceAfter, input.RequiresHr));
        await Audit("LeavePolicy.VersionCreated", row.Id);
        return Dto(row);
    });
    [Authorize(WorkforcePermissions.LeaveOwn)]
    public virtual async Task<List<HolidayInput>> GetHolidaysAsync(int year)
    {
        Require(year is >= 2000 and <= 2100, "Invalid year.");
        return (await holidays.GetListAsync(x => x.Date.Year == year)).Select(x => new HolidayInput(x.Date, x.Name)).ToList();
    }

    [Authorize(WorkforcePermissions.LeaveConfigure)]
    public virtual async Task AddHolidayAsync(HolidayInput input)
    {
        Require(input.Date != default && !string.IsNullOrWhiteSpace(input.Name), "Holiday date and name required.");
        var row = await holidays.InsertAsync(New(new PublicHoliday { Date = input.Date, Name = input.Name }));
        await Audit("Holiday.Created", row.Id);
    }

    [Authorize(WorkforcePermissions.LeaveConfigure), SerializableCommand]
    public virtual async Task SetAllocationAsync(AllocationInput input)
    {
        await InSerializableTransaction(async () =>
        {
            await Worker(input.EmployeeId);
            var policy = await Get(policies, input.PolicyId);
            Require(input.Year is >= 2000 and <= 2100 && input.Allocation >= 0 && input.Carry >= 0 && input.Carry <= policy.CarryLimit && !string.IsNullOrWhiteSpace(input.Reason), "Invalid leave allocation.");
            var row = await allocations.FindAsync(x => x.EmployeeId == input.EmployeeId && x.PolicyId == input.PolicyId && x.Year == input.Year);
            if (row == null)
                row = await allocations.InsertAsync(new LeaveAllocation(TenantId, input.EmployeeId, input.PolicyId, input.Year));
            row.SetAmounts(input.Allocation, input.Carry, input.Adjustment, policy.CarryLimit, Actor, input.Reason);
            await allocations.UpdateAsync(row);
            Require((await Available(input.EmployeeId, policy, input.Year) ?? 0) >= 0, "Allocation is below reserved leave.");
            await Audit("LeaveAllocation.Updated", row.Id, input.Reason);
        });
    }

    [Authorize(WorkforcePermissions.LeaveOwn)]
    public virtual async Task<List<BalanceDto>> GetBalancesAsync(Guid employeeId, int year)
    {
        await View(employeeId, true);
        Require(year is >= 2000 and <= 2100, "Invalid year.");
        var result = new List<BalanceDto>();
        foreach (var p in await policies.GetListAsync())
        {
            var configured = !p.Limited || await allocations.AnyAsync(x => x.EmployeeId == employeeId && x.PolicyId == p.Id && x.Year == year);
            result.Add(new(p.Id, p.Name, configured, !p.Limited, configured ? await Available(employeeId, p, year) : null));
        }

        return result;
    }

    async Task<decimal?> Available(Guid employee, AbsencePolicy policy, int year)
    {
        if (!policy.Limited)
            return null;
        var a = await allocations.FindAsync(x => x.EmployeeId == employee && x.PolicyId == policy.Id && x.Year == year) ?? throw new Volo.Abp.UserFriendlyException("Leave allocation is not configured.");
        var grant = WorkforceRules.Grant(a.Allocation, a.Carry, a.Adjustment, policy.MonthlyAccrual, year, Today);
        var used = (await requests.GetListAsync(x => x.EmployeeId == employee && x.PolicyId == policy.Id && x.Start.Year <= year && x.End.Year >= year && (x.Status == "Pending" || x.Status == "Approved" || x.Status == "Cancellation pending"))).Sum(x => x.ReservedDaysIn(year));
        return grant - used;
    }

    [Authorize(WorkforcePermissions.LeaveOwn)]
    public virtual async Task<Page<LeaveDto>> GetListAsync(PageQuery input)
    {
        PageBounds(input);
        var ids = await Queries.ToListAsync((await Visible()).Select(x => x.Id));
        var q = (await requests.GetQueryableAsync()).Where(x => ids.Contains(x.EmployeeId));
        return new(await Queries.LongCountAsync(q), (await Queries.ToListAsync(q.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount))).Select(Dto).ToList());
    }

    [Authorize(WorkforcePermissions.LeaveOwn), SerializableCommand]
    public virtual async Task<LeaveDto> SubmitAsync(LeaveInput input, Guid? employeeId = null)
    {
        return await InSerializableTransaction<LeaveDto>(async () =>
        {
            var id = employeeId ?? Own;
            Allow(id.HasValue && (Hr || id == Own));
            var employee = await Worker(id!.Value);
            var policy = await Get(policies, input.PolicyId);
            Require(policy.Active && input.Start != default && input.Start <= input.End && Active(employee, input.Start) && Active(employee, input.End) && !string.IsNullOrWhiteSpace(input.Reason), "Invalid leave dates, employee or policy.");
            Require(input.Start >= Today.AddDays(policy.NoticeDays), "Notice requirement not met.");
            await calendar.Editable(input.Start, input.End);
            var dates = await calendar.WorkingDates(employee.Id, input.Start, input.End);
            var days = dates.Count;
            Require(days > 0, "No working days selected.");
            if (policy.EvidenceAfter > 0 && days >= policy.EvidenceAfter)
            {
                Require(input.EvidenceId.HasValue, "Evidence is required.");
                var doc = await Get(documents, input.EvidenceId!.Value);
                Require(doc.EmployeeId == employee.Id && doc.Kind == "LeaveEvidence", "Own leave evidence required.");
            }

            Require(!await requests.AnyAsync(x => x.EmployeeId == employee.Id && (x.Status == "Pending" || x.Status == "Approved" || x.Status == "Cancellation pending") && x.Start <= input.End && x.End >= input.Start), "Overlapping leave exists.");
            foreach (var year in dates.GroupBy(x => x.Year))
            {
                var available = await Available(employee.Id, policy, year.Key);
                Require(available == null || available >= year.Count(), "Insufficient leave balance for " + year.Key);
            }

            var row = await requests.InsertAsync(new Absence(TenantId, employee.Id, policy.Id, input.Start, input.End, dates, policy.Paid, policy.RequiresHr, input.Reason, input.EvidenceId, employee.ManagerId.HasValue));
            await Audit("Leave.Submitted", row.Id);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.LeaveReview), SerializableCommand]
    public virtual async Task<LeaveDto> ReviewAsync(Guid id, DecisionInput input)
    {
        return await InSerializableTransaction<LeaveDto>(async () =>
        {
            var row = await Get(requests, id);
            await Review(row.EmployeeId);
            Allow(row.Stage != "HR" || Hr);
            Stamp(row, input.ConcurrencyStamp);
            Require(row.Status is "Pending" or "Cancellation pending" && input.Status is "Approved" or "Rejected" && !string.IsNullOrWhiteSpace(input.Note), "Invalid leave review.");
            await calendar.Editable(row.Start, row.End);
            if (row.Status != "Cancellation pending" && input.Status == "Approved")
            {
                Require(!await sessions.AnyAsync(x => x.EmployeeId == row.EmployeeId && x.Status != "Rejected" && x.Date >= row.Start && x.Date <= row.End), "Resolve overlapping work attendance.");
                var policy = await Get(policies, row.PolicyId);
                foreach (var year in row.WorkingDates.Select(x => x.Year).Distinct())
                    Require((await Available(row.EmployeeId, policy, year) ?? 0) >= 0, "Leave balance requires review.");
            }

            row.Decide(input.Status == "Approved", Actor, Hr, input.Note);
            await requests.UpdateAsync(row);
            await Audit("Leave.Reviewed." + input.Status, id, input.Note);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.LeaveOwn), SerializableCommand]
    public virtual async Task<LeaveDto> WithdrawAsync(Guid id, string concurrencyStamp)
    {
        return await InSerializableTransaction<LeaveDto>(async () =>
        {
            var row = await Get(requests, id);
            Allow(row.EmployeeId == Own);
            Stamp(row, concurrencyStamp);
            Require(row.Status == "Pending", "Only pending leave can be withdrawn.");
            row.Withdraw();
            await requests.UpdateAsync(row);
            await Audit("Leave.Withdrawn", id);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.LeaveOwn), SerializableCommand]
    public virtual async Task<LeaveDto> RequestCancellationAsync(Guid id, string concurrencyStamp)
    {
        return await InSerializableTransaction<LeaveDto>(async () =>
        {
            var row = await Get(requests, id);
            Allow(row.EmployeeId == Own);
            Stamp(row, concurrencyStamp);
            Require(row.Status == "Approved" && row.Start > Today, "Only future approved leave can request cancellation.");
            await calendar.Editable(row.Start, row.End);
            row.RequestCancellation(Today, (await Worker(row.EmployeeId)).ManagerId.HasValue);
            await requests.UpdateAsync(row);
            await Audit("Leave.CancellationRequested", id);
            await Flush();
            return Dto(row);
        });
    }

    static PolicyDto Dto(AbsencePolicy x) => new(x.Id, x.Name, x.Version, x.Active, x.Paid, x.Limited, x.AnnualDays, x.MonthlyAccrual, x.CarryLimit, x.NoticeDays, x.EvidenceAfter, x.RequiresHr);
    static LeaveDto Dto(Absence x) => new(x.Id, x.EmployeeId, x.PolicyId, x.Start, x.End, x.Days, x.Paid, x.Status, x.Stage, x.Reason, x.ConcurrencyStamp);
}
