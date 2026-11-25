using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class WorkforceAudit : WorkforceEntity
{
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Note { get; set; } = "";
}
