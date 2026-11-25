using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class WorkSchedule : WorkforceRow
{
    public Guid EmployeeId { get; set; }
    public int DaysMask { get; set; } = 62;
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public int GraceMinutes { get; set; }
}
