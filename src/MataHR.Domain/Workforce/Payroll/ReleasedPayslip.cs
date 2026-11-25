using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class ReleasedPayslip : WorkforceRow
{
    public Guid EmployeeId { get; set; }
    public Guid RunId { get; set; }
    public Guid DocumentId { get; set; }
    public bool Released { get; set; }
}
