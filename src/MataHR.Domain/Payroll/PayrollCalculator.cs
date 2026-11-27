using MataHR.Attendance;
using MataHR.Common;
using MataHR.Leave;
using Volo.Abp.Domain.Services;

namespace MataHR.Payroll;
public record PayrollTotals(decimal EmployerBenefits, decimal EmployeeBenefits, decimal Hours, decimal OvertimeHours, decimal OvertimePay, int UnpaidDays, int Pending);
public class PayrollCalculator : DomainService
{
    public PayrollTotals Calculate(Compensation pay, IReadOnlyList<EmployeeBenefit> benefits, IReadOnlyList<WorkSession> sessions, IReadOnlyList<TimeRequest> timeRequests, IReadOnlyList<LeaveRequest> leave, DateOnly from, DateOnly to)
    {
        DomainGuard.Against(pay.EffectiveFrom > from, "CompensationNotEffective", "Compensation is not effective in this period.");
        decimal hours = decimal.Round((decimal)sessions.Where(x => x.Status == "Approved" && x.End.HasValue).Sum(x => (x.End!.Value - x.Start).TotalHours), 2);
        decimal overtime = decimal.Round((decimal)timeRequests.Where(x => x.Kind == "Overtime" && x.Status == "Approved").Sum(x => (x.End - x.Start).TotalHours), 2);
        int pending = sessions.Count(x => x.Status is "Open" or "Pending review") + timeRequests.Count(x => x.Status == "Pending") + leave.Count(x => x.Status is "Pending" or "Cancellation pending");
        int unpaid = leave.Where(x => x.Status == "Approved" && !x.Paid).Sum(x => x.WorkingDates.Count(d => d >= from && d <= to));
        return new(benefits.Sum(x => x.Employer), benefits.Sum(x => x.EmployeeCost), hours, overtime, decimal.Round(overtime * pay.HourlyRate * pay.OvertimeMultiplier, 2, MidpointRounding.AwayFromZero), unpaid, pending);
    }
}
