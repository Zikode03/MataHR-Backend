using Volo.Abp.Authorization.Permissions;

namespace MataHR.Permissions;
public static class WorkforcePermissions
{
    public const string Dashboard = "Workforce.Dashboard";
    public const string PeopleRead = "Workforce.People.Read";
    public const string PeopleWrite = "Workforce.People.Write";
    public const string OrganisationRead = "Workforce.Organisation.Read";
    public const string OrganisationWrite = "Workforce.Organisation.Write";
    public const string AttendanceOwn = "Workforce.Attendance.Own";
    public const string AttendanceReview = "Workforce.Attendance.Review";
    public const string AttendanceConfigure = "Workforce.Attendance.Configure";
    public const string LeaveOwn = "Workforce.Leave.Own";
    public const string LeaveReview = "Workforce.Leave.Review";
    public const string LeaveConfigure = "Workforce.Leave.Configure";
    public const string PayrollOwn = "Workforce.Payroll.Own";
    public const string PayrollManage = "Workforce.Payroll.Manage";
    public const string Accounts = "Workforce.Accounts";
    public const string Audit = "Workforce.Audit";
    public static readonly string[] All = [Dashboard, PeopleRead, PeopleWrite, OrganisationRead, OrganisationWrite, AttendanceOwn, AttendanceReview, AttendanceConfigure, LeaveOwn, LeaveReview, LeaveConfigure, PayrollOwn, PayrollManage, Accounts, Audit];

}

public class WorkforcePermissionDefinitions : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup("Workforce");
        foreach (var name in WorkforcePermissions.All)
            group.AddPermission(name);
    }
}
