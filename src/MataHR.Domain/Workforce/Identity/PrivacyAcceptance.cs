using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class PrivacyAcceptance : WorkforceEntity
{
    public Guid AccountId { get; set; }
    public string Version { get; set; } = "";
}
