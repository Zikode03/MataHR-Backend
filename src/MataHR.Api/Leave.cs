using Microsoft.EntityFrameworkCore;
using static MataHR.Access;
namespace MataHR;
public record PolicyInput(string Name,bool Paid,bool Limited,decimal AnnualDays,bool MonthlyAccrual,decimal CarryLimit,int NoticeDays,int EvidenceAfter,bool RequiresHr);
public record EntitlementInput(Guid EmployeeId,Guid PolicyId,int Year,decimal Allocation,decimal Carry,decimal Adjustment,string Reason);
public record LeaveInput(Guid EmployeeId,Guid PolicyId,DateOnly Start,DateOnly End,string Reason,string? EvidenceReference);
public record HolidayInput(DateOnly Date,string Name);
public static class LeaveApi{
 static async Task<decimal?> Available(Db db,HttpContext c,Guid employee,LeavePolicy p,int year){
 if(!p.Limited)return null;
 var e=await db.Scoped<Entitlement>(c).SingleOrDefaultAsync(x=>x.EmployeeId==employee&&x.PolicyId==p.Id&&x.Year==year)??throw new RuleException("Entitlement not configured.");
 var today=Today();int months=year<today.Year?12:year>today.Year?0:today.Month;
 decimal granted=(p.MonthlyAccrual?Math.Floor(e.Allocation*months/12*100)/100:e.Allocation)+e.Carry+e.Adjustment;
 int used=await db.Scoped<LeaveRequest>(c).Where(x=>x.EmployeeId==employee&&x.PolicyId==p.Id&&x.Start.Year==year&&(x.Status=="Pending"||x.Status=="Approved")).SumAsync(x=>x.Days);
 return granted-used;
 }
 public static void MapLeave(this RouteGroupBuilder api){
 api.MapGet("/leave/policies",async(Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)!=null);return await db.Scoped<LeavePolicy>(c).ToListAsync();});
 api.MapPost("/leave/policies",async(PolicyInput i,Db db,HttpContext c)=>{Need(Hr(c));await Internal(db,c,"leave");Require(!string.IsNullOrWhiteSpace(i.Name)&&i.AnnualDays>=0&&i.CarryLimit>=0&&i.NoticeDays>=0&&i.EvidenceAfter>=0,"Invalid leave policy.");var p=new LeavePolicy{TenantId=Tenant(c),Name=i.Name,Paid=i.Paid,Limited=i.Limited,AnnualDays=i.AnnualDays,MonthlyAccrual=i.MonthlyAccrual,CarryLimit=i.CarryLimit,NoticeDays=i.NoticeDays,EvidenceAfter=i.EvidenceAfter,RequiresHr=i.RequiresHr};db.Add(p);Log(db,c,"LeavePolicyCreated",p.Id);await db.SaveChangesAsync();return Results.Ok(p);});
 api.MapPost("/leave/holidays",async(HolidayInput i,Db db,HttpContext c)=>{Need(Hr(c));await Internal(db,c,"leave");Require(i.Date!=default&&!string.IsNullOrWhiteSpace(i.Name),"Invalid holiday.");var h=new Holiday{TenantId=Tenant(c),Date=i.Date,Name=i.Name};db.Add(h);Log(db,c,"HolidayCreated",h.Id);await db.SaveChangesAsync();return Results.Ok(h);});
 api.MapPut("/leave/entitlements",async(EntitlementInput i,Db db,HttpContext c)=>{
 Need(Hr(c));await Internal(db,c,"leave");await FindEmployee(db,c,i.EmployeeId);var p=await db.Scoped<LeavePolicy>(c).SingleOrDefaultAsync(x=>x.Id==i.PolicyId)??throw new RuleException("Policy not found",404);Require(i.Year is >=2000 and <=2100&&i.Allocation>=0&&i.Carry>=0&&i.Carry<=p.CarryLimit&&!string.IsNullOrWhiteSpace(i.Reason),"Invalid entitlement.");
 await using var tx=await db.Database.BeginTransactionAsync();var e=await db.Scoped<Entitlement>(c).SingleOrDefaultAsync(x=>x.EmployeeId==i.EmployeeId&&x.PolicyId==i.PolicyId&&x.Year==i.Year);if(e==null){e=new Entitlement{TenantId=Tenant(c),EmployeeId=i.EmployeeId,PolicyId=i.PolicyId,Year=i.Year};db.Add(e);}e.Allocation=i.Allocation;e.Carry=i.Carry;e.Adjustment=i.Adjustment;await db.SaveChangesAsync();Require((await Available(db,c,i.EmployeeId,p,i.Year)??0)>=0,"Entitlement below reserved leave.");Log(db,c,"EntitlementUpdated",e.Id,i.Reason);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(e);
 });
 api.MapGet("/leave/balances/{employee:guid}",async(Guid employee,int year,Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)==employee);var rows=new List<object>();foreach(var p in await db.Scoped<LeavePolicy>(c).ToListAsync()){decimal? available=null;bool configured=!p.Limited||await db.Scoped<Entitlement>(c).AnyAsync(x=>x.EmployeeId==employee&&x.PolicyId==p.Id&&x.Year==year);if(configured)available=await Available(db,c,employee,p,year);rows.Add(new{policy=p.Name,policyId=p.Id,configured,unlimited=!p.Limited,available});}return Results.Ok(rows);});
 api.MapGet("/leave/requests",async(Db db,HttpContext c)=>{var ids=await Visible(db,c);return await db.Scoped<LeaveRequest>(c).Where(x=>ids.Contains(x.EmployeeId)).ToListAsync();});
 api.MapPost("/leave/requests",async(LeaveInput i,Db db,HttpContext c)=>{
 Need(Hr(c)||Employee(c)==i.EmployeeId);await Internal(db,c,"leave");await using var tx=await db.Database.BeginTransactionAsync();var e=await FindEmployee(db,c,i.EmployeeId);var p=await db.Scoped<LeavePolicy>(c).SingleOrDefaultAsync(x=>x.Id==i.PolicyId&&x.Active)??throw new RuleException("Active policy required.");
 Require(i.Start.Year==i.End.Year&&Active(e,i.Start)&&Active(e,i.End)&&i.Start<=i.End,"Invalid leave period.");if(p.NoticeDays>0)Require(i.Start>=Today().AddDays(p.NoticeDays),"Notice requirement not met.");int days=await Days(db,c,e.Id,i.Start,i.End);Require(days>0&&(p.EvidenceAfter==0||days<p.EvidenceAfter||!string.IsNullOrWhiteSpace(i.EvidenceReference)),"Working days and required evidence needed.");
 Need(!await db.Scoped<LeaveRequest>(c).AnyAsync(x=>x.EmployeeId==e.Id&&x.Status!="Rejected"&&x.Status!="Withdrawn"&&x.Start<=i.End&&x.End>=i.Start),"Overlapping leave.",409);var balance=await Available(db,c,e.Id,p,i.Start.Year);Require(balance==null||balance>=days,"Insufficient entitlement.");
 var row=new LeaveRequest{TenantId=Tenant(c),EmployeeId=e.Id,PolicyId=p.Id,Start=i.Start,End=i.End,Days=days,Paid=p.Paid,RequiresHr=p.RequiresHr,Reason=i.Reason??"",EvidenceReference=i.EvidenceReference??""};db.Add(row);Log(db,c,"LeaveSubmitted",row.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
 });
 api.MapPost("/leave/requests/{id:guid}/review",async(Guid id,Decision i,Db db,HttpContext c)=>{
 await Internal(db,c,"leave");await using var tx=await db.Database.BeginTransactionAsync();var row=await db.Scoped<LeaveRequest>(c).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Request not found",404);
 Need(await CanReview(db,c,row.EmployeeId)&&(row.Stage!="HR"||Hr(c)));Require(row.Status=="Pending"&&new[]{"Approved","Rejected"}.Contains(i.Status)&&!string.IsNullOrWhiteSpace(i.Note),"Invalid decision.");
 if(i.Status=="Approved"){Need(!await db.Scoped<Session>(c).AnyAsync(x=>x.EmployeeId==row.EmployeeId&&x.Date>=row.Start&&x.Date<=row.End&&x.Status!="Rejected"),"Resolve overlapping attendance.",409);var p=await db.Scoped<LeavePolicy>(c).SingleAsync(x=>x.Id==row.PolicyId);Require((await Available(db,c,row.EmployeeId,p,row.Start.Year)??0)>=0,"Entitlement requires review.");}
 if(i.Status=="Approved"&&row.RequiresHr&&row.Stage=="Manager")row.Stage="HR";else{row.Status=i.Status;row.Stage="Complete";}Log(db,c,"LeaveReview"+i.Status,id,i.Note);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
 });
 api.MapPost("/leave/requests/{id:guid}/withdraw",async(Guid id,Db db,HttpContext c)=>{await Internal(db,c,"leave");await using var tx=await db.Database.BeginTransactionAsync();var row=await db.Scoped<LeaveRequest>(c).SingleOrDefaultAsync(x=>x.Id==id&&x.EmployeeId==Employee(c))??throw new RuleException("Request not found",404);Require(row.Status=="Pending","Only pending leave can be withdrawn.");row.Status="Withdrawn";row.Stage="Complete";Log(db,c,"LeaveWithdrawn",id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);});
 }
}
