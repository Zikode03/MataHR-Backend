using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
public class AttendanceAppService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits, IRepository<ClockLocation, Guid> locations, IRepository<WorkSchedule, Guid> schedules, IRepository<WorkSession, Guid> sessions, IRepository<TimeRequest, Guid> requests, IRepository<Absence, Guid> leave, CalendarService calendar, IRepository<PresenceChallenge, Guid> challenges) : WorkforceService(workers, audits), IAttendanceAppService
{
    [Authorize(WorkforcePermissions.AttendanceOwn)]
    public virtual async Task<SessionDto?> GetCurrentAsync()
    {
        Allow(Own.HasValue);
        var row = await sessions.FindAsync(x => x.EmployeeId == Own && x.Status == "Open");
        return row == null ? null : Dto(row);
    }

    [Authorize(WorkforcePermissions.AttendanceOwn)]
    public virtual async Task<List<LocationDto>> GetLocationsAsync() => (await locations.GetListAsync(x => x.Active)).Select(x => new LocationDto(x.Id, x.Name, x.Kind, x.Active, x.Latitude, x.Longitude, x.RadiusMeters)).ToList();
    [Authorize(WorkforcePermissions.AttendanceConfigure)]
    public virtual async Task<LocationDto> CreateLocationAsync(LocationInput input)
    {
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160 && new[] { "Office", "Remote", "Client site", "Business travel", "Training venue" }.Contains(input.Kind), "Invalid work location.");
        var row = await locations.InsertAsync(New(new ClockLocation { Name = input.Name.Trim(), Kind = input.Kind }));
        await Audit("Location.Created", row.Id);
        return new(row.Id, row.Name, row.Kind, row.Active);
    }

    [Authorize(WorkforcePermissions.AttendanceConfigure)]
    public virtual async Task<LocationDto> ConfigureBoundaryAsync(Guid id, SiteBoundaryInput input)
    {
        var site = await Get(locations, id);
        site.ConfigureBoundary(input.Latitude, input.Longitude, input.RadiusMeters);
        await locations.UpdateAsync(site);
        await Audit("Attendance.SiteBoundaryChanged", id);
        return new(site.Id, site.Name, site.Kind, site.Active, site.Latitude, site.Longitude, site.RadiusMeters);
    }

    [Authorize(WorkforcePermissions.AttendanceConfigure)]
    public virtual async Task<PresenceCodeDto> GeneratePresenceCodeAsync(Guid id) => await InSerializableTransaction<PresenceCodeDto>(async () =>
    {
        var site = await Get(locations, id);
        Require(site.Active && site.Latitude.HasValue, "Configure an active site's GPS boundary first.");
        foreach (var old in await challenges.GetListAsync(x => x.LocationId == id && x.ExpiresAt > Now))
        {
            old.Expire(Now);
            await challenges.UpdateAsync(old);
        }

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(10000000, 100000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var expires = Now.AddMinutes(2);
        await challenges.InsertAsync(new PresenceChallenge(TenantId, id, code, expires));
        await Audit("Attendance.SiteCodeRotated", id);
        return new(code, expires);
    });
    [Authorize(WorkforcePermissions.AttendanceOwn)]
    public virtual async Task<List<ScheduleDto>> GetSchedulesAsync()
    {
        var ids = await Queries.ToListAsync((await Visible()).Select(x => x.Id));
        return (await schedules.GetListAsync(x => ids.Contains(x.EmployeeId))).Select(x => new ScheduleDto(x.EmployeeId, x.DaysMask, x.Start, x.End, x.GraceMinutes)).ToList();
    }

    [Authorize(WorkforcePermissions.AttendanceConfigure)]
    public virtual async Task<ScheduleDto> SetScheduleAsync(ScheduleInput input)
    {
        await Worker(input.EmployeeId);
        Require(input.DaysMask is> 0 and <= 127 && input.Start < input.End && input.GraceMinutes is >= 0 and <= 120, "Invalid same-day schedule.");
        var row = await schedules.FindAsync(x => x.EmployeeId == input.EmployeeId);
        if (row == null)
            row = await schedules.InsertAsync(New(new WorkSchedule { EmployeeId = input.EmployeeId }));
        row.DaysMask = input.DaysMask;
        row.Start = input.Start;
        row.End = input.End;
        row.GraceMinutes = input.GraceMinutes;
        await schedules.UpdateAsync(row);
        await Audit("Schedule.Updated", row.Id);
        return new(row.EmployeeId, row.DaysMask, row.Start, row.End, row.GraceMinutes);
    }

    [Authorize(WorkforcePermissions.AttendanceOwn)]
    public virtual async Task<Page<SessionDto>> GetListAsync(DateOnly from, DateOnly to, PageQuery input)
    {
        PageBounds(input);
        Require(from <= to && to.DayNumber - from.DayNumber <= 366, "Invalid date range.");
        var ids = await Queries.ToListAsync((await Visible()).Select(x => x.Id));
        var q = (await sessions.GetQueryableAsync()).Where(x => ids.Contains(x.EmployeeId) && x.Date >= from && x.Date <= to);
        return new(await Queries.LongCountAsync(q), (await Queries.ToListAsync(q.OrderByDescending(x => x.Start).ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount))).Select(Dto).ToList());
    }

    [Authorize(WorkforcePermissions.AttendanceOwn), SerializableCommand]
    public virtual async Task<SessionDto> ClockInAsync(ClockInput input)
    {
        return await InSerializableTransaction<SessionDto>(async () =>
        {
            Allow(Own.HasValue);
            var employee = await Worker(Own!.Value);
            var now = Now;
            var date = WorkforceRules.WorkDate(now);
            Require(Active(employee, date), "Employee is not active.");
            await calendar.Editable(date, date);
            var location = await Get(locations, input.LocationId);
            Require(location.Active, "Active location required.");
            Require(!await sessions.AnyAsync(x => x.EmployeeId == employee.Id && x.Status == "Open"), "Open session already exists.");
            Require(!await leave.AnyAsync(x => x.EmployeeId == employee.Id && x.Status == "Approved" && x.Start <= date && x.End >= date), "Approved leave conflicts with clock-in.");
            var row = new WorkSession(TenantId, employee.Id, location.Id, now);
            if (!string.IsNullOrWhiteSpace(input.ExceptionReason))
                row.RecordException(input.ExceptionReason);
            else if (location.Kind is "Remote" or "Business travel")
                row.RecordException("Remote work — independent attendance review required.");
            else
            {
                Require(input.Latitude.HasValue && input.Longitude.HasValue && input.AccuracyMeters.HasValue && input.ObservedAt.HasValue && !string.IsNullOrWhiteSpace(input.SiteCode), "Use a current on-site code and device location, or submit an attendance exception for review.");
                Require(input.SiteCode!.Length == 8 && input.SiteCode.All(c => c >= '0' && c <= '9'), "Enter the eight-digit on-site code.");
                var challenge = (await challenges.GetListAsync(x => x.LocationId == location.Id && x.ExpiresAt > now)).OrderByDescending(x => x.ExpiresAt).FirstOrDefault();
                Require(challenge != null && challenge.Accepts(input.SiteCode!, now), "The on-site code is invalid or expired.");
                Require(!await sessions.AnyAsync(x => x.EmployeeId == employee.Id && x.PresenceChallengeId == challenge!.Id), "This on-site code has already been used by your account.");
                var distance = PresenceRules.Validate(location, input.Latitude!.Value, input.Longitude!.Value, input.AccuracyMeters!.Value, input.ObservedAt!.Value, now);
                row.RecordPresence(challenge!.Id, distance, input.AccuracyMeters.Value);
            }

            await sessions.InsertAsync(row);
            await Audit("Attendance.ClockIn", row.Id);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.AttendanceOwn), SerializableCommand]
    public virtual async Task<SessionDto> ClockOutAsync()
    {
        return await InSerializableTransaction<SessionDto>(async () =>
        {
            Allow(Own.HasValue);
            var row = await sessions.FindAsync(x => x.EmployeeId == Own && x.Status == "Open") ?? throw new Volo.Abp.UserFriendlyException("No open work session.");
            await calendar.Editable(row.Date, row.Date);
            row.ClockOut(Now);
            await sessions.UpdateAsync(row);
            await Audit("Attendance.ClockOut", row.Id);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.AttendanceReview), SerializableCommand]
    public virtual async Task<SessionDto> ReviewAsync(Guid id, DecisionInput input)
    {
        return await InSerializableTransaction<SessionDto>(async () =>
        {
            var row = await Get(sessions, id);
            await Review(row.EmployeeId);
            Stamp(row, input.ConcurrencyStamp);
            Require(row.Status == "Pending review" && row.End != null && input.Status is "Approved" or "Rejected" && !string.IsNullOrWhiteSpace(input.Note), "Invalid attendance decision.");
            await calendar.Editable(row.Date, row.Date);
            if (input.Status == "Approved")
                Require(!await leave.AnyAsync(x => x.EmployeeId == row.EmployeeId && x.Status == "Approved" && x.Start <= row.Date && x.End >= row.Date), "Resolve overlapping approved leave.");
            row.Review(input.Status == "Approved", Actor, Own, input.Note);
            await sessions.UpdateAsync(row);
            await Audit("Attendance." + input.Status, id, input.Note);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.AttendanceOwn)]
    public virtual async Task<List<TimeRequestDto>> GetRequestsAsync(DateOnly from, DateOnly to)
    {
        Require(from <= to && to.DayNumber - from.DayNumber <= 366, "Invalid range.");
        var ids = await Queries.ToListAsync((await Visible()).Select(x => x.Id));
        return (await requests.GetListAsync(x => ids.Contains(x.EmployeeId) && x.Date >= from && x.Date <= to)).Select(Dto).ToList();
    }

    [Authorize(WorkforcePermissions.AttendanceOwn), SerializableCommand]
    public virtual async Task<TimeRequestDto> SubmitRequestAsync(TimeRequestInput input)
    {
        return await InSerializableTransaction<TimeRequestDto>(async () =>
        {
            Allow(Own.HasValue);
            Require(input.Kind is "Correction" or "Overtime" && !string.IsNullOrWhiteSpace(input.Reason) && input.Start < input.End && input.End <= Now && input.End - input.Start <= TimeSpan.FromHours(24) && WorkforceRules.WorkDate(input.Start) == input.Date, "Invalid time request.");
            await calendar.Editable(input.Date, input.Date);
            Require(input.SessionId.HasValue, "An existing work session is required.");
            var session = await Get(sessions, input.SessionId.GetValueOrDefault());
            Require(session.EmployeeId == Own && session.Date == input.Date && session.End != null, "Completed own work session required.");
            Require(!await requests.AnyAsync(x => x.SessionId == session.Id && x.Status == "Pending"), "A request for this session is pending.");
            if (input.Kind == "Overtime")
            {
                Require(session.Status == "Approved" && input.Start >= session.Start && input.End <= session.End, "Overtime must be within an approved session.");
                Require(!await requests.AnyAsync(x => x.SessionId == session.Id && x.Kind == "Overtime" && x.Status != "Rejected" && x.Start < input.End && x.End > input.Start), "Overlapping overtime request.");
            }
            else
                Require(!await requests.AnyAsync(x => x.SessionId == session.Id && x.Kind == "Overtime" && x.Status != "Rejected"), "Resolve overtime before correcting the session.");
            var row = await requests.InsertAsync(new TimeRequest(TenantId, Own.GetValueOrDefault(), session.Id, input.Kind, input.Date, input.Start, input.End, input.Reason, Now));
            await Audit("TimeRequest.Submitted", row.Id);
            await Flush();
            return Dto(row);
        });
    }

    [Authorize(WorkforcePermissions.AttendanceReview), SerializableCommand]
    public virtual async Task<TimeRequestDto> ReviewRequestAsync(Guid id, DecisionInput input)
    {
        return await InSerializableTransaction<TimeRequestDto>(async () =>
        {
            var row = await Get(requests, id);
            await Review(row.EmployeeId);
            Stamp(row, input.ConcurrencyStamp);
            Require(row.Status == "Pending" && input.Status is "Approved" or "Rejected" && !string.IsNullOrWhiteSpace(input.Note), "Invalid request review.");
            await calendar.Editable(row.Date, row.Date);
            var session = await Get(sessions, row.SessionId.GetValueOrDefault());
            if (input.Status == "Approved")
            {
                Require(!await leave.AnyAsync(x => x.EmployeeId == row.EmployeeId && x.Status == "Approved" && x.Start <= row.Date && x.End >= row.Date), "Approved leave conflicts with attendance.");
                if (row.Kind == "Correction")
                {
                    Require(!await sessions.AnyAsync(x => x.EmployeeId == row.EmployeeId && x.Id != session.Id && x.Status != "Rejected" && x.Start < row.End && (x.End == null || x.End > row.Start)), "Correction overlaps another work session.");
                    session.Correct(row.Start, row.End, Actor, Own, input.Note);
                    await sessions.UpdateAsync(session);
                }
                else
                    Require(session.Status == "Approved" && row.Start >= session.Start && row.End <= session.End, "Overtime no longer matches approved attendance.");
            }

            row.Decide(input.Status == "Approved", Actor, Own, input.Note);
            await requests.UpdateAsync(row);
            await Audit("TimeRequest." + input.Status, id, input.Note);
            await Flush();
            return Dto(row);
        });
    }

    static SessionDto Dto(WorkSession x) => new(x.Id, x.EmployeeId, x.LocationId, x.Date, x.Start, x.End, x.Status, x.Evidence, x.ConcurrencyStamp, x.ExceptionReason, x.DistanceMeters, x.AccuracyMeters);
    static TimeRequestDto Dto(TimeRequest x) => new(x.Id, x.EmployeeId, x.Kind, x.SessionId, x.Date, x.Start, x.End, x.Reason, x.Status, x.ConcurrencyStamp);
}
