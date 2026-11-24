using Microsoft.EntityFrameworkCore;
using static MataHR.Access;
namespace MataHR;
public record LocationInput(string Name,string Kind);
public record ScheduleInput(Guid EmployeeId,int DaysMask,TimeOnly Start,TimeOnly End,int Grace);
public record ClockInput(Guid LocationId);
public record Decision(string Status,string Note);
public static class AttendanceApi{
 public static void MapAttendance(this RouteGroupBuilder api){
 api.MapGet("/attendance/locations",async(Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)!=null);return await db.Scoped<WorkLocation>(c).ToListAsync();});
 api.MapPost("/attendance/locations",async(LocationInput i,Db db,HttpContext c)=>{Need(Hr(c));Require(!string.IsNullOrWhiteSpace(i.Name)&&new[]{"Office","Remote","Client site","Business travel","Training venue"}.Contains(i.Kind),"Invalid location.");var row=new WorkLocation{TenantId=Tenant(c),Name=i.Name,Kind=i.Kind};db.Add(row);Log(db,c,"LocationCreated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapGet("/attendance/schedules",async(Db db,HttpContext c)=>{var ids=await Visible(db,c);return await db.Scoped<Schedule>(c).Where(x=>ids.Contains(x.EmployeeId)).ToListAsync();});
 api.MapPut("/attendance/schedules",async(ScheduleInput i,Db db,HttpContext c)=>{Need(Hr(c));await FindEmployee(db,c,i.EmployeeId);Require(i.DaysMask is >0 and <=127&&i.Start<i.End&&i.Grace is >=0 and <=120,"Invalid same-day schedule.");var row=await db.Scoped<Schedule>(c).SingleOrDefaultAsync(x=>x.EmployeeId==i.EmployeeId);if(row==null){row=new Schedule{TenantId=Tenant(c),EmployeeId=i.EmployeeId};db.Add(row);}row.DaysMask=i.DaysMask;row.Start=i.Start;row.End=i.End;row.Grace=i.Grace;Log(db,c,"ScheduleUpdated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapGet("/attendance",async(DateOnly from,DateOnly to,Db db,HttpContext c)=>{Require(from<=to&&to.DayNumber-from.DayNumber<=366,"Invalid range.");var ids=await Visible(db,c);return await db.Scoped<Session>(c).Where(x=>ids.Contains(x.EmployeeId)&&x.Date>=from&&x.Date<=to).ToListAsync();});
 api.MapPost("/attendance/clock-in",async(ClockInput i,Db db,HttpContext c)=>{
 Need(Employee(c)!=null);var id=Employee(c)!.Value;await using var tx=await db.Database.BeginTransactionAsync();var e=await FindEmployee(db,c,id);var now=DateTimeOffset.UtcNow;var date=DateOnly.FromDateTime(now.UtcDateTime.AddHours(2));
 Require(Active(e,date),"Employee is not active.");Require(await db.Scoped<WorkLocation>(c).AnyAsync(x=>x.Id==i.LocationId&&x.Active),"Active location required.");
 Need(!await db.Scoped<Session>(c).AnyAsync(x=>x.EmployeeId==id&&x.Status=="Open"),"Open session already exists.",409);
 Need(!await db.Scoped<LeaveRequest>(c).AnyAsync(x=>x.EmployeeId==id&&x.Status=="Approved"&&x.Start<=date&&x.End>=date),"Approved leave exists.",409);
 var row=new Session{TenantId=Tenant(c),EmployeeId=id,LocationId=i.LocationId,Date=date,Start=now};db.Add(row);Log(db,c,"ClockIn",row.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
 });
 api.MapPost("/attendance/clock-out",async(Db db,HttpContext c)=>{Need(Employee(c)!=null);await using var tx=await db.Database.BeginTransactionAsync();var row=await db.Scoped<Session>(c).SingleOrDefaultAsync(x=>x.EmployeeId==Employee(c)&&x.Status=="Open")??throw new RuleException("No open session.",409);row.End=DateTimeOffset.UtcNow;row.Status="Pending review";Log(db,c,"ClockOut",row.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);});
 api.MapPost("/attendance/{id:guid}/review",async(Guid id,Decision i,Db db,HttpContext c)=>{await using var tx=await db.Database.BeginTransactionAsync();var row=await db.Scoped<Session>(c).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Session not found",404);Need(await CanReview(db,c,row.EmployeeId));Require(row.Status=="Pending review"&&row.End!=null&&new[]{"Approved","Rejected"}.Contains(i.Status)&&!string.IsNullOrWhiteSpace(i.Note),"Invalid review.");if(i.Status=="Approved")Need(!await db.Scoped<LeaveRequest>(c).AnyAsync(x=>x.EmployeeId==row.EmployeeId&&x.Status=="Approved"&&x.Start<=row.Date&&x.End>=row.Date),"Conflicting leave.",409);row.Status=i.Status;Log(db,c,"Attendance"+i.Status,row.Id,i.Note);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);});
 }
}
