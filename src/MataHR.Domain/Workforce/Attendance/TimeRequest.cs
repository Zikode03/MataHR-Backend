namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class TimeRequest : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string Kind { get; private set; } = "Correction";
    public DateOnly Date { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public string Reason { get; private set; } = "";

    [Volo.Abp.Auditing.Audited]
    public string Status { get; private set; } = "Pending";
    public Guid? ReviewedBy { get; private set; }
    public string ReviewNote { get; private set; } = "";

    protected TimeRequest()
    {
    }

    public TimeRequest(Guid tenant, Guid employee, Guid session, string kind, DateOnly date, DateTimeOffset start, DateTimeOffset end, string reason, DateTimeOffset now)
    {
        DomainGuard.Against(kind is not ("Correction" or "Overtime") || start >= end || end > now || end - start > TimeSpan.FromHours(24) || WorkforceRules.WorkDate(start) != date, "InvalidTimeRequest", "Invalid correction or overtime period.");
        TenantId = tenant;
        EmployeeId = employee;
        SessionId = session;
        Kind = kind;
        Date = date;
        Start = start;
        End = end;
        Reason = DomainGuard.Text(reason, "TimeRequestReason", 2000);
    }

    public void Decide(bool approve, Guid actor, Guid? actorEmployee, string note)
    {
        DomainGuard.Against(actorEmployee == EmployeeId, "SelfApproval", "You cannot approve your own time request.");
        DomainGuard.Against(Status != "Pending", "InvalidTimeRequestTransition", "Request has already been reviewed.");
        ReviewNote = DomainGuard.Text(note, "ReviewNote", 2000);
        Status = approve ? "Approved" : "Rejected";
        ReviewedBy = actor;
    }
}
