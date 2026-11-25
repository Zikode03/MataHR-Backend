using System.ComponentModel.DataAnnotations;

namespace MataHR.Workforce.Contracts;
public class PageQuery
{
    [Range(0, int.MaxValue)]
    public int SkipCount { get; set; }

    [Range(1, 200)]
    public int MaxResultCount { get; set; } = 50;

    [StringLength(100)]
    public string? Search { get; set; }
}

public record Page<T>(long TotalCount, IReadOnlyList<T> Items);
public record EmployeeDto(Guid Id, string Number, string Name, string Email, DateOnly Started, DateOnly? Exited, string ContractType, Guid? ManagerId, Guid? PositionId, string ConcurrencyStamp);
public record EmployeeInput(string Number, string Name, string Email, DateOnly Started, DateOnly? Exited, string ContractType, Guid? ManagerId, Guid? PositionId, string? ConcurrencyStamp = null);
public record EmployeeHistoryDto(Guid Id, DateOnly EffectiveDate, string Description, string BeforeStateJson = "", string AfterStateJson = "");
public record BranchDto(Guid Id, string Name);
public record DepartmentDto(Guid Id, Guid BranchId, string Name);
public record PositionDto(Guid Id, Guid DepartmentId, string Name);
public record StructureDto(IReadOnlyList<BranchDto> Branches, IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<PositionDto> Positions, IReadOnlyList<OrganisationPersonDto> Employees, IReadOnlyList<LayoutDto> Layout, ChartPointDto? Company = null);
public record BranchInput(string Name);
public record DepartmentInput(Guid BranchId, string Name);
public record PositionInput(Guid DepartmentId, string Name);
public record AssignmentInput(Guid EmployeeId, Guid? PositionId, Guid? ManagerId, string ConcurrencyStamp);
public record LayoutDto(Guid EmployeeId, double X, double Y, string Connector);
public record ContractDto(Guid Id, Guid EmployeeId, string Reference, DateOnly Start, DateOnly? End, string Status, int Version, Guid? DocumentId, Guid? SignedDocumentId, string ConcurrencyStamp);
public record ContractInput(Guid EmployeeId, string Reference, DateOnly Start, DateOnly? End);
public record ContractDecision(string Status, string Note, Guid? DocumentId, string ConcurrencyStamp);
public record DocumentInput(Guid EmployeeId, string Name, string ContentType, string ContentBase64, string Kind = "Employee", bool EmployeeVisible = true, DateOnly? Expires = null);
public record DocumentDto(Guid Id, Guid EmployeeId, string Name, string Kind, string ContentType, int Size, string Sha256, DateOnly? Expires, bool Verified);
public record DocumentContent(string Name, string ContentType, string ContentBase64, string Sha256);
public record LocationDto(Guid Id, string Name, string Kind, bool Active, double? Latitude = null, double? Longitude = null, int RadiusMeters = 150);
public record LocationInput(string Name, string Kind);
public record ScheduleInput(Guid EmployeeId, int DaysMask, TimeOnly Start, TimeOnly End, int GraceMinutes);
public record ScheduleDto(Guid EmployeeId, int DaysMask, TimeOnly Start, TimeOnly End, int GraceMinutes);
public record SessionDto(Guid Id, Guid EmployeeId, Guid LocationId, DateOnly Date, DateTimeOffset Start, DateTimeOffset? End, string Status, string Evidence, string ConcurrencyStamp, string ExceptionReason = "", double? DistanceMeters = null, double? AccuracyMeters = null);
public record ClockInput(Guid LocationId, string? SiteCode = null, double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null, DateTimeOffset? ObservedAt = null, string? ExceptionReason = null);
public record DecisionInput(string Status, string Note, string ConcurrencyStamp);
public record TimeRequestInput(string Kind, Guid? SessionId, DateOnly Date, DateTimeOffset Start, DateTimeOffset End, string Reason);
public record TimeRequestDto(Guid Id, Guid EmployeeId, string Kind, Guid? SessionId, DateOnly Date, DateTimeOffset Start, DateTimeOffset End, string Reason, string Status, string ConcurrencyStamp);
public record PolicyInput(string Name, bool Paid, bool Limited, decimal AnnualDays, bool MonthlyAccrual, decimal CarryLimit, int NoticeDays, int EvidenceAfter, bool RequiresHr);
public record PolicyDto(Guid Id, string Name, int Version, bool Active, bool Paid, bool Limited, decimal AnnualDays, bool MonthlyAccrual, decimal CarryLimit, int NoticeDays, int EvidenceAfter, bool RequiresHr);
public record HolidayInput(DateOnly Date, string Name);
public record AllocationInput(Guid EmployeeId, Guid PolicyId, int Year, decimal Allocation, decimal Carry, decimal Adjustment, string Reason);
public record BalanceDto(Guid PolicyId, string Name, bool Configured, bool Unlimited, decimal? Available);
public record LeaveInput(Guid PolicyId, DateOnly Start, DateOnly End, string Reason, Guid? EvidenceId = null);
public record LeaveDto(Guid Id, Guid EmployeeId, Guid PolicyId, DateOnly Start, DateOnly End, int Days, bool Paid, string Status, string Stage, string Reason, string ConcurrencyStamp);
public record CompensationInput(Guid EmployeeId, decimal Basic, decimal Allowance, decimal Deduction, decimal HourlyRate, decimal OvertimeMultiplier, DateOnly EffectiveFrom);
public record CompensationDto(Guid EmployeeId, decimal Basic, decimal Allowance, decimal Deduction, decimal HourlyRate, decimal OvertimeMultiplier, DateOnly EffectiveFrom);
public record BenefitInput(Guid EmployeeId, string Name, decimal Employer, decimal EmployeeCost, DateOnly EffectiveFrom, DateOnly? EffectiveTo = null);
public record BenefitDto(Guid Id, Guid EmployeeId, string Name, decimal Employer, decimal EmployeeCost, bool Active, DateOnly EffectiveFrom, DateOnly? EffectiveTo = null);
public record PayrollRow(Guid EmployeeId, string Number, decimal Basic, decimal Allowance, decimal Deduction, decimal EmployerBenefits, decimal EmployeeBenefits, decimal ApprovedHours, decimal OvertimeHours, decimal OvertimePay, int UnpaidDays, int Pending, string SourceFingerprint = "");
public record PayrollReadiness(string Period, bool Ready, IReadOnlyList<string> Issues, IReadOnlyList<PayrollRow> Rows);
public record RunDto(Guid Id, string Period, string Status, string Hash, Guid? PreparedBy, Guid? ReviewedBy, Guid? ApprovedBy, string ConcurrencyStamp);
public record RunInput(string Period);
public record ExportDto(string Period, string Hash, string ContentJson);
public record PayslipInput(Guid EmployeeId, Guid DocumentId);
public record PayslipDto(Guid Id, Guid EmployeeId, Guid RunId, Guid DocumentId, bool Released);
public record DashboardDto(DateOnly From, DateOnly To, int Headcount, int NewJoiners, int RecordedToday, int OnLeaveToday, int PendingLeave, int PendingAttendance, int PendingTimeRequests, decimal? PreparedPayrollInputs);
public record AccountInput(string Email, string Password, string[] Roles, Guid? EmployeeId);
public record AccountDto(Guid Id, string Email, string[] Roles, Guid? EmployeeId, bool Active, string ConcurrencyStamp);
public record AccountUpdateInput(string[] Roles, Guid? EmployeeId, bool Active, string ConcurrencyStamp);
public record DocumentVerificationInput(string Purpose = "General", string Attestation = "", DateOnly? SignedOn = null);
public record RolePermissionInput(string Role, string Permission, bool Granted);
public record SiteBoundaryInput(double Latitude, double Longitude, int RadiusMeters);
public record PresenceCodeDto(string Code, DateTimeOffset ExpiresAt);
public record ChartPointDto(double X, double Y);
public record EmployeeLinkDto(Guid Id, string Number, string Name);
public record RoleGrantDto(string Permission, bool Granted);
public record OrganisationPersonDto(Guid Id, string Number, string Name, Guid? PositionId, Guid? ManagerId, bool Active, string ConcurrencyStamp);
