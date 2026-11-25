using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class Job : WorkforceRow
{
    public Guid DepartmentId { get; set; }
    public string Name { get; set; } = "";
}
