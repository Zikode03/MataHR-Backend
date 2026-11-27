using MataHR.Common;
namespace MataHR.Organisation;

// The company header position belongs to the chart layout rather than an employee’s reporting assignment.
[Volo.Abp.Auditing.DisableAuditing]
public class OrganisationCanvas : WorkforceRow
{
    public double X { get; private set; } = 400;
    public double Y { get; private set; }

    public void Move(double x, double y)
    {
        DomainGuard.Against(!ChartCoordinates.IsValid(x, y), "InvalidCanvasCoordinates", "Invalid canvas coordinates.");
        X = x;
        Y = y;
    }
}
