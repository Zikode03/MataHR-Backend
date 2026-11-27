namespace MataHR.Leave.Contracts;
public record PolicyInput(string Name, bool Paid, bool Limited, decimal AnnualDays, bool MonthlyAccrual, decimal CarryLimit, int NoticeDays, int EvidenceAfter, bool RequiresHr);
public record PolicyDto(Guid Id, string Name, int Version, bool Active, bool Paid, bool Limited, decimal AnnualDays, bool MonthlyAccrual, decimal CarryLimit, int NoticeDays, int EvidenceAfter, bool RequiresHr);
public record HolidayInput(DateOnly Date, string Name);
public record AllocationInput(Guid EmployeeId, Guid PolicyId, int Year, decimal Allocation, decimal Carry, decimal Adjustment, string Reason);
public record BalanceDto(Guid PolicyId, string Name, bool Configured, bool Unlimited, decimal? Available);
public record LeaveInput(Guid PolicyId, DateOnly Start, DateOnly End, string Reason, Guid? EvidenceId = null);
public record LeaveDto(Guid Id, Guid EmployeeId, Guid PolicyId, DateOnly Start, DateOnly End, int Days, bool Paid, string Status, string Stage, string Reason, string ConcurrencyStamp);
