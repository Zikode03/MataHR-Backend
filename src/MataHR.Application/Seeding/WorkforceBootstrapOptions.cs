namespace MataHR.Seeding;

// These settings are for the first account on a new database. Existing accounts are managed through Identity.
public class WorkforceBootstrapOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}
