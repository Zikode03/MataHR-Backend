using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Leave;

// Holiday dates feed the working-day calendar used by leave calculations.
[Volo.Abp.Auditing.DisableAuditing]
public class PublicHoliday : WorkforceRow
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}
