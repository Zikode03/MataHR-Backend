using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class ClockLocation : WorkforceRow
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Office";
    public bool Active { get; set; } = true;
}
