using MataHR.Foundation;
namespace MataHR.Domain {
public static class EmploymentRules
{
    public static void ValidateReporting(Guid employee, Guid? manager, IReadOnlyDictionary<Guid, Guid?> links)
    {
        var seen = new HashSet<Guid>
        {
            employee
        };
        while (manager is Guid id)
        {
            if (!seen.Add(id))
                throw new DomainRuleException("Circular reporting relationship.");
            if (!links.TryGetValue(id, out manager))
                throw new DomainRuleException("Reporting manager does not exist in this organisation.");
        }
    }

    public static bool Active(DateOnly started, DateOnly? exited, DateOnly on) => started <= on && (exited is null || exited > on);
}

public enum ContractStage
{
    Draft,
    InReview,
    Approved,
    AwaitingSignature,
    Signed
}

public static class ContractRules
{
    public static void ValidateTransition(ContractStage current, ContractStage next, bool attached, bool verifiedSignedCopy, string? note, Guid actor, Guid? previousReviewer)
    {
        if ((current, next)is not ((ContractStage.Draft, ContractStage.InReview) or (ContractStage.InReview, ContractStage.Approved) or (ContractStage.Approved, ContractStage.AwaitingSignature) or (ContractStage.AwaitingSignature, ContractStage.Signed)))
            throw new DomainRuleException("Invalid contract transition.");
        if (!attached)
            throw new DomainRuleException("Contract document required.");
        if (next == ContractStage.Approved && (string.IsNullOrWhiteSpace(note) || actor == previousReviewer))
            throw new DomainRuleException("Independent approval and review note required.");
        if (next == ContractStage.Signed && !verifiedSignedCopy)
            throw new DomainRuleException("Verified signed evidence required.");
    }
}

}
namespace MataHR.Contracts {
public record OrganisationAssignmentInput(Guid EmployeeId, Guid PositionId, Guid? ManagerId);
public record BranchInput(string Name);
public record DepartmentInput(Guid BranchId, string Name);
public record PositionInput(Guid DepartmentId, string Name);
public record OrganisationNode(Guid EmployeeId, string EmployeeNumber, string Name, Guid PositionId, string Position, Guid DepartmentId, string Department, Guid BranchId, string Branch, Guid? ManagerId);
public record OrganisationSnapshot(IReadOnlyList<OrganisationNode> Assignments);
public interface IOrganisationRules
{
    void ValidateAssignment(Guid employee, Guid? manager, IReadOnlyDictionary<Guid, Guid?> links);
}

}
namespace MataHR.Foundation {
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

}
namespace MataHR.Application { public sealed class OrganisationRules : MataHR.Contracts.IOrganisationRules { public void ValidateAssignment(Guid employee, Guid? manager, IReadOnlyDictionary<Guid,Guid?> links) => MataHR.Domain.EmploymentRules.ValidateReporting(employee,manager,links); } }
