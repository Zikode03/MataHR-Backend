namespace MataHR.Workforce;
[Volo.Abp.Auditing.DisableAuditing]
public class WorkSession : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public Guid LocationId { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset? End { get; private set; }

    [Volo.Abp.Auditing.Audited]
    public string Status { get; private set; } = "Open";
    public string Evidence { get; private set; } = "Unverified";
    public Guid? PresenceChallengeId { get; private set; }
    public double? DistanceMeters { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public string ExceptionReason { get; private set; } = "";

    public void RecordPresence(Guid challenge, double distance, double accuracy)
    {
        DomainGuard.Against(challenge == Guid.Empty || !double.IsFinite(distance) || distance < 0 || !double.IsFinite(accuracy) || accuracy < 0 || accuracy > 50, "InvalidPresenceEvidence", "Invalid presence evidence.");
        PresenceChallengeId = challenge;
        DistanceMeters = Math.Round(distance, 1);
        AccuracyMeters = Math.Round(accuracy, 1);
        Evidence = "On-site code + GPS checked";
    }

    public void RecordException(string reason)
    {
        ExceptionReason = DomainGuard.Text(reason, "PresenceExceptionReason", 1000);
        Evidence = "Unverified";
    }

    public Guid? ReviewedBy { get; private set; }
    public string ReviewNote { get; private set; } = "";

    protected WorkSession()
    {
    }

    public WorkSession(Guid tenant, Guid employee, Guid location, DateTimeOffset start)
    {
        DomainGuard.Against(employee == Guid.Empty || location == Guid.Empty, "InvalidClockSession", "Employee and location required.");
        TenantId = tenant;
        EmployeeId = employee;
        LocationId = location;
        Start = start;
        Date = WorkforceRules.WorkDate(start);
    }

    public void ClockOut(DateTimeOffset now)
    {
        DomainGuard.Against(Status != "Open" || now < Start, "InvalidClockOut", "An open session and valid server time are required.");
        End = now;
        Status = "Pending review";
    }

    public void Review(bool approve, Guid actor, Guid? actorEmployee, string note)
    {
        DomainGuard.Against(actorEmployee == EmployeeId, "SelfApproval", "You cannot review your own attendance.");
        DomainGuard.Against(Status != "Pending review" || End == null, "InvalidAttendanceReview", "Only completed attendance awaiting review can be reviewed.");
        ReviewNote = DomainGuard.Text(note, "ReviewNote", 2000);
        ReviewedBy = actor;
        Status = approve ? "Approved" : "Rejected";
    }

    public void Correct(DateTimeOffset start, DateTimeOffset end, Guid actor, Guid? actorEmployee, string note)
    {
        DomainGuard.Against(actorEmployee == EmployeeId, "SelfApproval", "You cannot approve your own correction.");
        DomainGuard.Against(Status == "Open" || start >= end || end - start > TimeSpan.FromHours(24) || WorkforceRules.WorkDate(start) != Date, "InvalidCorrection", "Invalid corrected attendance period.");
        ReviewNote = DomainGuard.Text(note, "ReviewNote", 2000);
        Start = start;
        End = end;
        Status = "Approved";
        ReviewedBy = actor;
    }
}
