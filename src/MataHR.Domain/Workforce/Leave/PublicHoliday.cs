using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class PublicHoliday : WorkforceRow
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}
