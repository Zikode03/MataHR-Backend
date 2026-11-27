using MataHR.Common;
using System.Text.Json;

namespace MataHR.Organisation;
[Volo.Abp.Auditing.DisableAuditing]
public class ChartCard : WorkforceEntity
{
    public Guid EmployeeId { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public string Connector { get; private set; } = "[]";

    protected ChartCard() { }

    public ChartCard(Guid employeeId, double x, double y, string connector)
    {
        EmployeeId = employeeId;
        Move(x, y, connector);
    }

    public void Move(double x, double y, string connector)
    {
        DomainGuard.Against(!ChartCoordinates.IsValid(x, y) || connector is null || connector.Length > 4000,
            "InvalidChartCoordinates", "Invalid chart coordinates.");

        // Validate all bends before changing state so a rejected edit leaves the card intact.
        try
        {
            using var json = JsonDocument.Parse(connector!);
            var points = json.RootElement;
            DomainGuard.Against(points.ValueKind != JsonValueKind.Array || points.GetArrayLength() > 50,
                "InvalidConnector", "Connector points must be an array.");
            foreach (var point in points.EnumerateArray())
            {
                var valid = point.ValueKind == JsonValueKind.Object
                    && point.TryGetProperty("x", out var bendX) && bendX.ValueKind == JsonValueKind.Number
                    && point.TryGetProperty("y", out var bendY) && bendY.ValueKind == JsonValueKind.Number
                    && bendX.TryGetDouble(out var px) && bendY.TryGetDouble(out var py)
                    && ChartCoordinates.IsValid(px, py);
                DomainGuard.Against(!valid, "InvalidConnector", "Invalid connector point.");
            }
        }
        catch (JsonException)
        {
            throw new WorkforceRuleException("InvalidConnector", "Invalid connector JSON.");
        }

        X = x;
        Y = y;
        Connector = connector!;
    }
}
