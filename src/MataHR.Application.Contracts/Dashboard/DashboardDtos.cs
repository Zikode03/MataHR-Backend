namespace MataHR.Dashboard.Contracts;
public record DashboardDto(DateOnly From, DateOnly To, int Headcount, int NewJoiners, int RecordedToday, int OnLeaveToday, int PendingLeave, int PendingAttendance, int PendingTimeRequests, decimal? PreparedPayrollInputs);
