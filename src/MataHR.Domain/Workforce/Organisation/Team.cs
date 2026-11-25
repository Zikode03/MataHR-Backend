using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class Team : WorkforceRow
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = "";
}
