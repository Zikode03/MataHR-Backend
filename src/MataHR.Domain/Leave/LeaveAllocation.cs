using MataHR.Common;
namespace MataHR.Leave;

// The balance ledger records changes over time so reservations and adjustments can be traced.
[Volo.Abp.Auditing.DisableAuditing]
public class LeaveAllocation : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public Guid PolicyId { get; private set; }
    public int Year { get; private set; }
    public decimal Allocation { get; private set; }
    public decimal Carry { get; private set; }
    public decimal Adjustment { get; private set; }

    private readonly List<LeaveBalanceEntry> _entries = [];
    public IReadOnlyCollection<LeaveBalanceEntry> Entries => _entries.AsReadOnly();

    protected LeaveAllocation()
    {
    }

    public LeaveAllocation(Guid tenant, Guid employee, Guid policy, int year)
    {
        DomainGuard.Against(year < 2000 || year > 2100, "InvalidLeaveYear", "Invalid leave allocation year.");
        TenantId = tenant;
        EmployeeId = employee;
        PolicyId = policy;
        Year = year;
    }

    public void SetAmounts(decimal allocation, decimal carry, decimal adjustment, decimal cap, Guid actor, string reason)
    {
        DomainGuard.Against(allocation < 0 || carry < 0 || carry > cap, "InvalidLeaveAllocation", "Invalid leave allocation or carry-over.");
        DomainGuard.Text(reason, "AdjustmentReason", 2000);
        var before = System.Text.Json.JsonSerializer.Serialize(new { Allocation, Carry, Adjustment });
        var delta = allocation + carry + adjustment - Allocation - Carry - Adjustment;
        Allocation = allocation;
        Carry = carry;
        Adjustment = adjustment;
        _entries.Add(new LeaveBalanceEntry(TenantId!.Value, Id, actor, delta, allocation + carry + adjustment, reason, before, System.Text.Json.JsonSerializer.Serialize(new { Allocation, Carry, Adjustment })));
    }
}

[Volo.Abp.Auditing.DisableAuditing]
public class LeaveBalanceEntry : WorkforceEntity
{
    public Guid AllocationId { get; private set; }
    public Guid ActorId { get; private set; }
    public decimal Delta { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string Reason { get; private set; } = "";
    public string BeforeStateJson { get; private set; } = "";
    public string AfterStateJson { get; private set; } = "";

    protected LeaveBalanceEntry()
    {
    }

    internal LeaveBalanceEntry(Guid tenant, Guid allocation, Guid actor, decimal delta, decimal balance, string reason, string before, string after)
    {
        TenantId = tenant;
        AllocationId = allocation;
        ActorId = actor;
        Delta = delta;
        BalanceAfter = balance;
        Reason = reason;
        BeforeStateJson = before;
        AfterStateJson = after;
    }
}
