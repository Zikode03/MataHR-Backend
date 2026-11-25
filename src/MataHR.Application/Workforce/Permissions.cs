using System.Security.Claims;
using Volo.Abp.Authorization.Permissions;

namespace MataHR.Workforce;
public static class WorkforcePermissions
{
    public const string Dashboard = "Workforce.Dashboard", PeopleRead = "Workforce.People.Read", PeopleWrite = "Workforce.People.Write", OrganisationRead = "Workforce.Organisation.Read", OrganisationWrite = "Workforce.Organisation.Write", AttendanceOwn = "Workforce.Attendance.Own", AttendanceReview = "Workforce.Attendance.Review", AttendanceConfigure = "Workforce.Attendance.Configure", LeaveOwn = "Workforce.Leave.Own", LeaveReview = "Workforce.Leave.Review", LeaveConfigure = "Workforce.Leave.Configure", PayrollOwn = "Workforce.Payroll.Own", PayrollManage = "Workforce.Payroll.Manage", Accounts = "Workforce.Accounts", Audit = "Workforce.Audit";
    public static readonly string[] All = [Dashboard, PeopleRead, PeopleWrite, OrganisationRead, OrganisationWrite, AttendanceOwn, AttendanceReview, AttendanceConfigure, LeaveOwn, LeaveReview, LeaveConfigure, PayrollOwn, PayrollManage, Accounts, Audit];
    public static bool Allows(IEnumerable<string> roles, string permission)
    {
        var own = new[]
        {
            Dashboard,
            PeopleRead,
            OrganisationRead,
            AttendanceOwn,
            LeaveOwn,
            PayrollOwn
        };
        return roles.Any(r => r switch
        {
            "Admin" => own.Contains(permission) || permission == Accounts || permission == Audit,
            "HR" => All.Contains(permission) && permission != Accounts,
            "Manager" => own.Contains(permission) || permission == AttendanceReview || permission == LeaveReview,
            "Employee" => own.Contains(permission),
            _ => false
        });
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

public class WorkforcePermissionSeeder(Volo.Abp.PermissionManagement.IPermissionDataSeeder grants, Volo.Abp.Identity.IdentityRoleManager roles, Volo.Abp.MultiTenancy.ICurrentTenant tenant) : Volo.Abp.DependencyInjection.ITransientDependency
{
    public async Task SeedAsync(Guid tenantId)
    {
        using (tenant.Change(tenantId))
        {
            foreach (var name in new[]
            {
                "Admin",
                "HR",
                "Manager",
                "Employee"
            }

            )
            {
                if (await roles.FindByNameAsync(name) == null)
                {
                    var result = await roles.CreateAsync(new Volo.Abp.Identity.IdentityRole(Guid.NewGuid(), name, tenantId));
                    if (!result.Succeeded)
                        throw new Volo.Abp.UserFriendlyException("Could not create role.");
                }

                await grants.SeedAsync(RolePermissionValueProvider.ProviderName, name, WorkforcePermissions.All.Where(p => WorkforcePermissions.Allows([name], p)), tenantId);
            }
        }
    }
}
