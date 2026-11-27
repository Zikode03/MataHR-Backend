using MataHR.Permissions;
namespace MataHR.Seeding;

// Defaults are used only when a role is created. Authorization uses the saved ABP grants.
public static class DefaultWorkforceRoleGrants
{
    public static bool Allows(IEnumerable<string> roles, string permission)
    {
        var own = new[]
        {
            WorkforcePermissions.Dashboard,
            WorkforcePermissions.PeopleRead,
            WorkforcePermissions.OrganisationRead,
            WorkforcePermissions.AttendanceOwn,
            WorkforcePermissions.LeaveOwn,
            WorkforcePermissions.PayrollOwn
        };
        return roles.Any(r => r switch
        {
            "Admin" => own.Contains(permission) || permission == WorkforcePermissions.Accounts || permission == WorkforcePermissions.Audit,
            "HR" => WorkforcePermissions.All.Contains(permission) && permission != WorkforcePermissions.Accounts,
            "Manager" => own.Contains(permission) || permission == WorkforcePermissions.AttendanceReview || permission == WorkforcePermissions.LeaveReview,
            "Employee" => own.Contains(permission),
            _ => false
        });
    }
}
