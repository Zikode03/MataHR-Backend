using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class ChartCard : WorkforceEntity
{
    public Guid EmployeeId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Connector { get; set; } = "[]";
}
