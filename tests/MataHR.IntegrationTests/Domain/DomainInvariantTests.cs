using MataHR.Common;
using MataHR.Leave;
using MataHR.Organisation;
using Xunit;

namespace MataHR.IntegrationTests;

// These tests exercise business rules directly without relying on an API request or a database.
public class DomainInvariantTests
{
    [Fact]
    public void InvalidConnectorLeavesChartCardAtItsLastValidPosition()
    {
        var card = new ChartCard(Guid.NewGuid(), 10, 20, "[]");
        Assert.Throws<WorkforceRuleException>(() => card.Move(30, 40, "[null]"));
        Assert.Equal(10, card.X);
        Assert.Equal(20, card.Y);
        Assert.Equal("[]", card.Connector);
    }

    [Fact]
    public void WorkingDatesIncludesTheLastRepresentableDay()
    {
        var end = DateOnly.MaxValue;
        Assert.Equal(new[] { end }, WorkforceRules.WorkingDates(end, end, 127, new HashSet<DateOnly>()));
    }

    static LeaveRequest CrossYear() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2026, 12, 31), new(2027, 1, 4), [new(2026, 12, 31), new(2027, 1, 4)], true, true, "Annual leave", null, true);
    [Fact]
    public void CrossYearLeaveReservesEachYearSeparately()
    {
        var leave = CrossYear();
        Assert.Equal(1, leave.ReservedDaysIn(2026));
        Assert.Equal(1, leave.ReservedDaysIn(2027));
    }

    [Fact]
    public void CancellationRequiresIndependentHrReview()
    {
        var leave = CrossYear();
        var manager = Guid.NewGuid();
        leave.Decide(true, manager, false, "Manager approved");
        Assert.Throws<WorkforceRuleException>(() => leave.Decide(true, manager, true, "Same reviewer"));
        leave.Decide(true, Guid.NewGuid(), true, "HR approved");
        leave.RequestCancellation(new(2026, 6, 15), true);
        leave.Decide(true, manager, false, "Cancel approved");
        Assert.Equal("Cancellation pending", leave.Status);
        Assert.Equal("HR", leave.Stage);
        leave.Decide(true, Guid.NewGuid(), true, "HR cancellation approved");
        Assert.Equal("Cancelled", leave.Status);
        Assert.Throws<WorkforceRuleException>(() => leave.Withdraw());
    }

    [Fact]
    public void LegacyLeaveWithoutFrozenCalendarFailsClosed()
    {
        var leave = CrossYear();
        typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.WorkingDatesJson))!.SetValue(leave, "[]");
        Assert.Throws<WorkforceRuleException>(() => leave.ReservedDaysIn(2026));
    }

    [Fact]
    public void AllocationRecordsDeltasAndBeforeAfterStates()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2026);
        var actor = Guid.NewGuid();
        allocation.SetAmounts(20, 0, 0, 5, actor, "Initial grant");
        allocation.SetAmounts(20, 2, -1, 5, actor, "Carry reconciliation");
        Assert.Equal(2, allocation.Entries.Count);
        Assert.Equal(1, allocation.Entries.Last().Delta);
        Assert.Equal(21, allocation.Entries.Last().BalanceAfter);
        Assert.Contains("20", allocation.Entries.Last().BeforeStateJson);
        Assert.Equal(actor, allocation.Entries.Last().ActorId);
    }
}
