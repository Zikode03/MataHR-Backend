using MataHR.Common;
using System.Security.Cryptography;
using System.Text;

namespace MataHR.Attendance;

// The server stores a hash of the short-lived code. Clock-in compares the submitted code against that hash.
[Volo.Abp.Auditing.DisableAuditing]
public class PresenceChallenge : WorkforceRow
{
    public Guid LocationId { get; private set; }
    public string CodeHash { get; private set; } = "";
    public DateTimeOffset ExpiresAt { get; private set; }

    protected PresenceChallenge()
    {
    }

    public PresenceChallenge(Guid tenant, Guid location, string code, DateTimeOffset expires)
    {
        TenantId = tenant;
        LocationId = location;
        CodeHash = Hash(code);
        ExpiresAt = expires;
    }

    public void Expire(DateTimeOffset now) => ExpiresAt = now;
    public bool Accepts(string code, DateTimeOffset now) => now < ExpiresAt && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeHash), Encoding.ASCII.GetBytes(Hash(code)));
    static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}

public static class PresenceRules
{
    public static double DistanceMeters(double latitude, double longitude, double targetLatitude, double targetLongitude)
    {
        static double R(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(R(targetLatitude - latitude) / 2), 2) + Math.Cos(R(latitude)) * Math.Cos(R(targetLatitude)) * Math.Pow(Math.Sin(R(targetLongitude - longitude) / 2), 2);
        return 6371000 * 2 * Math.Atan2(Math.Sqrt(Math.Clamp(a, 0, 1)), Math.Sqrt(1 - Math.Clamp(a, 0, 1)));
    }

    public static double Validate(ClockLocation site, double latitude, double longitude, double accuracy, DateTimeOffset observed, DateTimeOffset now)
    {
        DomainGuard.Against(!site.Latitude.HasValue || !site.Longitude.HasValue, "SiteNotConfigured", "HR must configure this site's GPS boundary.");
        DomainGuard.Against(!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180 || !double.IsFinite(accuracy) || accuracy < 0 || accuracy > 50, "PoorLocationAccuracy", "A valid location accurate to 50 metres or better is required.");
        DomainGuard.Against(observed < now.AddSeconds(-90) || observed > now.AddSeconds(15), "StaleLocation", "Request a fresh device location.");
        var distance = DistanceMeters(latitude, longitude, site.Latitude!.Value, site.Longitude!.Value);
        DomainGuard.Against(distance + accuracy > site.RadiusMeters, "OutsideSite", "Your measured location and accuracy must fall inside the configured site boundary.");
        return distance;
    }
}
