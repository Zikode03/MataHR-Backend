using MataHR.Seeding;
using MataHR.Common;
using MataHR.Permissions;
using Xunit;

namespace MataHR.IntegrationTests;

// Use fixed inputs to keep date, payroll and permission rule checks independent of the machine running them.
public class RuleTests
{
    [Fact]
    public void WorkingDaysExcludeWeekendsAndCompanyHolidays()
    {
        Assert.Equal(4, WorkforceRules.WorkingDays(new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 29), 62, new HashSet<DateOnly> { new(2026, 11, 25) }));
    }

    [Fact]
    public void ClockWorkDateUsesSouthAfricanDayBoundary()
    {
        Assert.Equal(new DateOnly(2026, 11, 26), WorkforceRules.WorkDate(new DateTimeOffset(2026, 11, 25, 22, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void FutureMonthlyAccrualDoesNotGrantFutureYearBalance()
    {
        Assert.Equal(0, WorkforceRules.Grant(24, 0, 0, true, 2027, new DateOnly(2026, 11, 25)));
        Assert.Equal(22, WorkforceRules.Grant(24, 0, 0, true, 2026, new DateOnly(2026, 11, 25)));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Employee")]
    [InlineData("Manager")]
    public void NonHrCannotManagePayroll(string role)
    {
        Assert.False(DefaultWorkforceRoleGrants.Allows([role], WorkforcePermissions.PayrollManage));
        Assert.True(DefaultWorkforceRoleGrants.Allows([role], WorkforcePermissions.AttendanceOwn));
        Assert.True(DefaultWorkforceRoleGrants.Allows([role], WorkforcePermissions.LeaveOwn));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("invalid")]
    public void PayrollRejectsMalformedPeriods(string period) => Assert.Throws<ArgumentException>(() => WorkforceRules.Period(period));
}
