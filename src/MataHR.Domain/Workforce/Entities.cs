using System.Text.Json.Serialization;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
namespace MataHR.Workforce;

public abstract class WorkforceRow : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId
    {
        get; set;
    }
    protected WorkforceRow() : base(Guid.NewGuid()) { }
}
public class Worker : WorkforceRow
{
    public string Number { get; set; } = ""; public string Name { get; set; } = ""; public string Email { get; set; } = "";
    public DateOnly Started
    {
        get; set;
    }
    public DateOnly? Exited
    {
        get; set;
    }
    public string ContractType { get; set; } = "Permanent";
    public Guid? ManagerId
    {
        get; set;
    }
    public Guid? PositionId
    {
        get; set;
    }
}
public class WorkforceUser : WorkforceRow
{
    public string Email { get; set; } = ""; [JsonIgnore] public string PasswordHash { get; set; } = "";
    public string Roles { get; set; } = "Employee"; public Guid? EmployeeId
    {
        get; set;
    }
    public bool Active { get; set; } = true;
    public int FailedLogins
    {
        get; set;
    }
    public DateTimeOffset? LockedUntil
    {
        get; set;
    }
}
public class Office : WorkforceRow
{
    public string Name { get; set; } = "";
}
public class Team : WorkforceRow
{
    public Guid BranchId
    {
        get; set;
    }
    public string Name { get; set; } = "";
}
public class Job : WorkforceRow
{
    public Guid DepartmentId
    {
        get; set;
    }
    public string Name { get; set; } = "";
}
public class ChartCard : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public double X
    {
        get; set;
    }
    public double Y
    {
        get; set;
    }
    public string Connector { get; set; } = "[]";
}
public class EmploymentChange : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public DateOnly EffectiveDate
    {
        get; set;
    }
    public string Description { get; set; } = "";
}
public class WorkContract : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public string Reference { get; set; } = ""; public DateOnly Start
    {
        get; set;
    }
    public DateOnly? End
    {
        get; set;
    }
    public string Status { get; set; } = "Draft"; public int Version { get; set; } = 1; public Guid? DocumentId
    {
        get; set;
    }
    public Guid? SignedDocumentId
    {
        get; set;
    }
    public Guid? SubmittedBy
    {
        get; set;
    }
    public Guid? ApprovedBy
    {
        get; set;
    }
}
public class SecureDocument : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public string Name { get; set; } = ""; public string Kind { get; set; } = "Employee";
    public string ContentType { get; set; } = "application/pdf"; [JsonIgnore] public byte[] Content { get; set; } = [];
    public string Sha256 { get; set; } = ""; public DateOnly? Expires
    {
        get; set;
    }
    public Guid? VerifiedBy
    {
        get; set;
    }
    public bool EmployeeVisible { get; set; } = true;
}
public class ClockLocation : WorkforceRow
{
    public string Name { get; set; } = ""; public string Kind { get; set; } = "Office"; public bool Active { get; set; } = true;
}
public class WorkSchedule : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public int DaysMask { get; set; } = 62; public TimeOnly Start
    {
        get; set;
    }
    public TimeOnly End
    {
        get; set;
    }
    public int GraceMinutes
    {
        get; set;
    }
}
public class WorkSession : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public Guid LocationId
    {
        get; set;
    }
    public DateOnly Date
    {
        get; set;
    }
    public DateTimeOffset Start
    {
        get; set;
    }
    public DateTimeOffset? End
    {
        get; set;
    }
    public string Status { get; set; } = "Open";
    public string Evidence { get; set; } = "Unverified"; public Guid? ReviewedBy
    {
        get; set;
    }
    public string ReviewNote { get; set; } = "";
}
public class TimeRequest : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public Guid? SessionId
    {
        get; set;
    }
    public string Kind { get; set; } = "Correction";
    public DateOnly Date
    {
        get; set;
    }
    public DateTimeOffset Start
    {
        get; set;
    }
    public DateTimeOffset End
    {
        get; set;
    }
    public string Reason { get; set; } = ""; public string Status { get; set; } = "Pending"; public Guid? ReviewedBy
    {
        get; set;
    }
    public string ReviewNote { get; set; } = "";
}
public class AbsencePolicy : WorkforceRow
{
    public string Name { get; set; } = ""; public int Version { get; set; } = 1; public bool Active { get; set; } = true; public bool Paid { get; set; } = true;
    public bool Limited { get; set; } = true; public decimal AnnualDays
    {
        get; set;
    }
    public bool MonthlyAccrual
    {
        get; set;
    }
    public decimal CarryLimit
    {
        get; set;
    }
    public int NoticeDays
    {
        get; set;
    }
    public int EvidenceAfter
    {
        get; set;
    }
    public bool RequiresHr
    {
        get; set;
    }
}
public class PublicHoliday : WorkforceRow
{
    public DateOnly Date
    {
        get; set;
    }
    public string Name { get; set; } = "";
}
public class LeaveAllocation : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public Guid PolicyId
    {
        get; set;
    }
    public int Year
    {
        get; set;
    }
    public decimal Allocation
    {
        get; set;
    }
    public decimal Carry
    {
        get; set;
    }
    public decimal Adjustment
    {
        get; set;
    }
}
public class Absence : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public Guid PolicyId
    {
        get; set;
    }
    public DateOnly Start
    {
        get; set;
    }
    public DateOnly End
    {
        get; set;
    }
    public int Days
    {
        get; set;
    }
    public bool Paid
    {
        get; set;
    }
    public bool RequiresHr
    {
        get; set;
    }
    public string Reason { get; set; } = ""; public Guid? EvidenceId
    {
        get; set;
    }
    public string Status { get; set; } = "Pending"; public string Stage { get; set; } = "Manager"; public Guid? ManagerReviewerId
    {
        get; set;
    }
    public Guid? HrReviewerId
    {
        get; set;
    }
}
public class Compensation : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public decimal Basic
    {
        get; set;
    }
    public decimal Allowance
    {
        get; set;
    }
    public decimal Deduction
    {
        get; set;
    }
    public decimal HourlyRate
    {
        get; set;
    }
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
}
public class EmployeeBenefit : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public string Name { get; set; } = ""; public decimal Employer
    {
        get; set;
    }
    public decimal EmployeeCost
    {
        get; set;
    }
    public bool Active { get; set; } = true;
}
public class PayRun : WorkforceRow
{
    public string Period { get; set; } = ""; public string Status { get; set; } = "Draft"; public string Snapshot { get; set; } = "[]"; public string Hash { get; set; } = "";
    public Guid? PreparedBy
    {
        get; set;
    }
    public Guid? ReviewedBy
    {
        get; set;
    }
    public Guid? ApprovedBy
    {
        get; set;
    }
}
public class ReleasedPayslip : WorkforceRow
{
    public Guid EmployeeId
    {
        get; set;
    }
    public Guid RunId
    {
        get; set;
    }
    public Guid DocumentId
    {
        get; set;
    }
    public bool Released
    {
        get; set;
    }
}
public class WorkforceAudit : WorkforceRow
{
    public Guid ActorId
    {
        get; set;
    }
    public string Action { get; set; } = ""; public Guid TargetId
    {
        get; set;
    }
    public string Note { get; set; } = "";
}
public class PrivacyAcceptance : WorkforceRow
{
    public Guid AccountId
    {
        get; set;
    }
    public string Version { get; set; } = "";
}
