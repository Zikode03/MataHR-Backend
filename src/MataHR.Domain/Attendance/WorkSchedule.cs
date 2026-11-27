using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Attendance;
[Volo.Abp.Auditing.DisableAuditing]
public class WorkSchedule : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public int DaysMask { get; private set; } = 62;
    public TimeOnly Start { get; private set; }
    public TimeOnly End { get; private set; }
    public int GraceMinutes { get; private set; }

    protected WorkSchedule() { }

    public WorkSchedule(Guid employeeId, int daysMask, TimeOnly start, TimeOnly end, int graceMinutes)
    {
        EmployeeId = employeeId;
        Configure(daysMask, start, end, graceMinutes);
    }

    // Overnight shifts need a different attendance model. A schedule here ends on the same day.
    public void Configure(int daysMask, TimeOnly start, TimeOnly end, int graceMinutes)
    {
        DomainGuard.Against(daysMask is < 1 or > 127 || start >= end || graceMinutes is < 0 or > 120,
            "InvalidSchedule", "Invalid same-day schedule.");
        DaysMask = daysMask;
        Start = start;
        End = end;
        GraceMinutes = graceMinutes;
    }
}
