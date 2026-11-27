using MataHR.Common;
using System.Text.Json;

namespace MataHR.Leave;

// Freeze the working dates when the request is made. A later calendar edit must not silently change a reservation.
[Volo.Abp.Auditing.DisableAuditing]
public class LeaveRequest : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public Guid PolicyId { get; private set; }
    public DateOnly Start { get; private set; }
    public DateOnly End { get; private set; }
    public int Days { get; private set; }
    public bool Paid { get; private set; }
    public bool RequiresHr { get; private set; }
    public string Reason { get; private set; } = "";
    public Guid? EvidenceId { get; private set; }

    [Volo.Abp.Auditing.Audited]
    public string Status { get; private set; } = "Pending";

    [Volo.Abp.Auditing.Audited]
    public string Stage { get; private set; } = "Manager";
    public Guid? ManagerReviewerId { get; private set; }
    public Guid? HrReviewerId { get; private set; }
    public string WorkingDatesJson { get; private set; } = "[]";

    public IReadOnlyList<DateOnly> WorkingDates
    {
        get
        {
            var dates = JsonSerializer.Deserialize<List<DateOnly>>(WorkingDatesJson) ?? [];
            DomainGuard.Against(dates.Count != Days || dates.Any(x => x < Start || x > End) || dates.Distinct().Count() != dates.Count, "LeaveCalendarReconciliationRequired", "Historical leave requires working-date reconciliation before balance or payroll processing.");
            return dates;
        }
    }

    protected LeaveRequest()
    {
    }

    public LeaveRequest(Guid tenant, Guid employee, Guid policy, DateOnly from, DateOnly to, IReadOnlyList<DateOnly> workingDates, bool paid, bool requiresHr, string reason, Guid? evidence, bool hasManager)
    {
        DomainGuard.Against(from == default || from > to || to.DayNumber - from.DayNumber > 366 || workingDates.Count == 0 || workingDates.Any(x => x < from || x > to) || workingDates.Distinct().Count() != workingDates.Count, "InvalidLeavePeriod", "Invalid leave period or working dates.");
        TenantId = tenant;
        EmployeeId = employee;
        PolicyId = policy;
        Start = from;
        End = to;
        Days = workingDates.Count;
        WorkingDatesJson = JsonSerializer.Serialize(workingDates);
        Paid = paid;
        RequiresHr = requiresHr;
        Reason = DomainGuard.Text(reason, "LeaveReason", 2000);
        EvidenceId = evidence;
        Stage = hasManager ? "Manager" : "HR";
    }

    public void Decide(bool approve, Guid actor, bool hr, string note)
    {
        DomainGuard.Text(note, "ReviewNote", 2000);
        DomainGuard.Against(Status is not ("Pending" or "Cancellation pending"), "InvalidLeaveTransition", "Leave is not awaiting a decision.");
        DomainGuard.Against(Stage == "HR" && !hr, "HrApprovalRequired", "HR approval is required.");
        if (Stage == "HR")
        {
            DomainGuard.Against(ManagerReviewerId == actor, "IndependentHrApprovalRequired", "HR review must be independent of manager review.");
            HrReviewerId = actor;
        }
        else
            ManagerReviewerId = actor;
        bool cancellation = Status == "Cancellation pending";
        if (approve && RequiresHr && Stage == "Manager")
        {
            Stage = "HR";
            return;
        }

        Status = cancellation ? (approve ? "Cancelled" : "Approved") : (approve ? "Approved" : "Rejected");
        Stage = "Complete";
    }

    public void Withdraw()
    {
        DomainGuard.Against(Status != "Pending", "InvalidLeaveTransition", "Only pending leave can be withdrawn.");
        Status = "Withdrawn";
        Stage = "Complete";
    }

    public void RequestCancellation(DateOnly today, bool hasManager)
    {
        DomainGuard.Against(Status != "Approved" || Start <= today, "InvalidLeaveTransition", "Only future approved leave can request cancellation.");
        Status = "Cancellation pending";
        Stage = hasManager ? "Manager" : "HR";
        ManagerReviewerId = null;
        HrReviewerId = null;
    }

    public int ReservedDaysIn(int year) => WorkingDates.Count(x => x.Year == year);
}
