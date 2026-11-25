using System.Security.Claims;
using Volo.Abp.Authorization.Permissions;
namespace MataHR.Workforce;

public static class WorkforcePermissions
{
    public const string Dashboard = "Workforce.Dashboard", PeopleRead = "Workforce.People.Read", PeopleWrite = "Workforce.People.Write", OrganisationRead = "Workforce.Organisation.Read", OrganisationWrite = "Workforce.Organisation.Write", AttendanceOwn = "Workforce.Attendance.Own", AttendanceReview = "Workforce.Attendance.Review", AttendanceConfigure = "Workforce.Attendance.Configure", LeaveOwn = "Workforce.Leave.Own", LeaveReview = "Workforce.Leave.Review", LeaveConfigure = "Workforce.Leave.Configure", PayrollOwn = "Workforce.Payroll.Own", PayrollManage = "Workforce.Payroll.Manage", Accounts = "Workforce.Accounts", Audit = "Workforce.Audit";
    public static readonly string[] All = [Dashboard, PeopleRead, PeopleWrite, OrganisationRead, OrganisationWrite, AttendanceOwn, AttendanceReview, AttendanceConfigure, LeaveOwn, LeaveReview, LeaveConfigure, PayrollOwn, PayrollManage, Accounts, Audit];
    public static bool Allows(IEnumerable<string> roles, string permission)
    {
        var own = new[] { Dashboard, PeopleRead, OrganisationRead, AttendanceOwn, LeaveOwn, PayrollOwn };
        return roles.Any(r => r switch { "Admin" => own.Contains(permission) || permission == Accounts || permission == Audit, "HR" => All.Contains(permission) && permission != Accounts, "Manager" => own.Contains(permission) || permission == AttendanceReview || permission == LeaveReview, "Employee" => own.Contains(permission), _ => false });
    }
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
public class WorkforceRolePermissionProvider(IPermissionStore store) : PermissionValueProvider(store)
{
    public override string Name => "MataHRRoles";
    public override Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
    {
        var result = new MultiplePermissionGrantResult();
        foreach (var p in context.Permissions)
            result.Result[p.Name] = WorkforcePermissions.Allows(context.Principal?.FindAll(ClaimTypes.Role).Select(x => x.Value) ?? [], p.Name) ? PermissionGrantResult.Granted : PermissionGrantResult.Undefined;
        return Task.FromResult(result);
    }
    public override Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context) => Task.FromResult(WorkforcePermissions.Allows(context.Principal?.FindAll(ClaimTypes.Role).Select(x => x.Value) ?? [], context.Permission.Name) ? PermissionGrantResult.Granted : PermissionGrantResult.Undefined);
}
