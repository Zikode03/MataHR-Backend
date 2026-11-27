using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Authentication;

// Store the notice version with the account because a revised notice may require another acknowledgement.
[Volo.Abp.Auditing.DisableAuditing]
public class PrivacyAcceptance : WorkforceEntity
{
    public Guid AccountId { get; set; }
    public string Version { get; set; } = "";
}
