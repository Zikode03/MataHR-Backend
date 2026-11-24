using Volo.Abp.Modularity;
namespace MataHR.Foundation;
public sealed class MataHrDomainSharedModule:AbpModule {}
public static class Permissions {
 public const string PeopleRead="People.Read", PeopleWrite="People.Write", OrganisationWrite="Organisation.Write", AttendanceOwn="Attendance.Own", AttendanceReview="Attendance.Review", LeaveOwn="Leave.Own", LeaveReview="Leave.Review", LeaveConfigure="Leave.Configure", PayrollPrepare="Payroll.Prepare", PayrollApprove="Payroll.Approve", PayrollOwn="Payroll.Own", AccountsManage="Accounts.Manage", AuditRead="Audit.Read", IntegrationsManage="Integrations.Manage";
 private static readonly Dictionary<string,string[]> Grants=new(StringComparer.Ordinal){
 ["Admin"]=[AccountsManage,AuditRead,IntegrationsManage],
 ["HR"]=[PeopleRead,PeopleWrite,OrganisationWrite,AttendanceReview,LeaveReview,LeaveConfigure,PayrollPrepare,PayrollApprove,IntegrationsManage],
 ["Manager"]=[PeopleRead,AttendanceReview,LeaveReview,AttendanceOwn,LeaveOwn,PayrollOwn],
 ["Employee"]=[PeopleRead,AttendanceOwn,LeaveOwn,PayrollOwn]};
 public static bool Allows(IEnumerable<string> roles,string permission)=>roles.Any(role=>Grants.TryGetValue(role,out var list)&&list.Contains(permission));
 public static IReadOnlyList<string> For(IEnumerable<string> roles)=>roles.SelectMany(role=>Grants.GetValueOrDefault(role,[])).Distinct().Order().ToArray();
}
public sealed class DomainRuleException(string message):Exception(message);
