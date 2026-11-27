using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Auditing;

// This entry records a business action such as approving leave or assigning a reporting line.
[Volo.Abp.Auditing.DisableAuditing]
public class WorkforceAudit : WorkforceEntity
{
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Note { get; set; } = "";
}
