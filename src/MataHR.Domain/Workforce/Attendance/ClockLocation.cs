using System.Text.Json.Serialization;

namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class ClockLocation : WorkforceRow
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Office";
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public int RadiusMeters { get; private set; } = 150;

    public void ConfigureBoundary(double latitude, double longitude, int radius)
    {
        DomainGuard.Against(!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180 || radius < 50 || radius > 1000, "InvalidSiteBoundary", "Use valid coordinates and a radius from 50 to 1000 metres.");
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radius;
    }

    public bool Active { get; set; } = true;
}
