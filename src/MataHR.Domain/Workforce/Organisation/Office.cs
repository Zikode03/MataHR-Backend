using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class Office : WorkforceRow
{
    public string Name { get; set; } = "";
}
