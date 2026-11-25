// Schema compatibility only. Authentication uses ABP IdentityUser; never create new accounts here.
using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class WorkforceUser : WorkforceRow
{
    public string Email { get; set; } = "";

    [JsonIgnore]
    public string PasswordHash { get; set; } = "";
    public string Roles { get; set; } = "Employee";
    public Guid? EmployeeId { get; set; }
    public bool Active { get; set; } = true;
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
