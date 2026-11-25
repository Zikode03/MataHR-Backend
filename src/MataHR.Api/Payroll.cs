using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static MataHR.Access;
namespace MataHR;
public record SalaryInput(Guid EmployeeId,decimal Basic,decimal Allowance,decimal Deduction);
public record BenefitInput(Guid EmployeeId,string Name,decimal Employer,decimal EmployeeCost);
public record RunInput(string Period);
public record PayRow(Guid EmployeeId,string Number,decimal Basic,decimal Allowance,decimal Deduction,decimal EmployerBenefit,decimal EmployeeBenefit,double ApprovedHours,int UnpaidDays,int Pending);
public static class PayrollApi{
 static (DateOnly Start,DateOnly End) Period(string period){Require(DateOnly.TryParseExact(period+"-01","yyyy-MM-dd",out var start),"Use YYYY-MM period.");return (start,start.AddMonths(1).AddDays(-1));}
 static async Task<List<PayRow>> Inputs(Db db,HttpContext c,string period){
 var (start,end)=Period(period);var people=await db.Scoped<Employee>(c).Where(x=>x.Started<=end&&(x.Exited==null||x.Exited>=start)).OrderBy(x=>x.Id).ToListAsync();Require(people.Count>0,"No employees.");var rows=new List<PayRow>();
 foreach(var e in people){
 var salary=await db.Scoped<Salary>(c).SingleOrDefaultAsync(x=>x.EmployeeId==e.Id)??throw new RuleException("Missing salary profile for "+e.Number);
 var benefits=await db.Scoped<Benefit>(c).Where(x=>x.EmployeeId==e.Id&&x.Active).ToListAsync();
 var sessions=await db.Scoped<Session>(c).Where(x=>x.EmployeeId==e.Id&&x.Date>=start&&x.Date<=end).ToListAsync();
 var leave=await db.Scoped<LeaveRequest>(c).Where(x=>x.EmployeeId==e.Id&&x.Start<=end&&x.End>=start).ToListAsync();int unpaid=0;
 foreach(var l in leave.Where(x=>x.Status=="Approved"&&!x.Paid))unpaid+=await Days(db,c,e.Id,l.Start>start?l.Start:start,l.End<end?l.End:end);
 var pending=sessions.Count(x=>x.Status=="Open"||x.Status=="Pending review")+leave.Count(x=>x.Status=="Pending");
 rows.Add(new(e.Id,e.Number,salary.Basic,salary.Allowance,salary.Deduction,benefits.Sum(x=>x.Employer),benefits.Sum(x=>x.EmployeeCost),sessions.Where(x=>x.Status=="Approved"&&x.End!=null).Sum(x=>(x.End!.Value-x.Start).TotalHours),unpaid,pending));
 }return rows;
 }
 public static void MapPayroll(this RouteGroupBuilder api){
 api.MapGet("/payroll/salaries",async(Db db,HttpContext c)=>{Need(Hr(c));return await db.Scoped<Salary>(c).ToListAsync();});
 api.MapPut("/payroll/salaries",async(SalaryInput i,Db db,HttpContext c)=>{Need(Hr(c));await Access.Internal(db,c,"payroll");await FindEmployee(db,c,i.EmployeeId);Require(i.Basic>=0&&i.Allowance>=0&&i.Deduction>=0,"Nonnegative amounts required.");var row=await db.Scoped<Salary>(c).SingleOrDefaultAsync(x=>x.EmployeeId==i.EmployeeId);if(row==null){row=new Salary{TenantId=Tenant(c),EmployeeId=i.EmployeeId};db.Add(row);}row.Basic=i.Basic;row.Allowance=i.Allowance;row.Deduction=i.Deduction;Log(db,c,"SalaryUpdated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapGet("/payroll/benefits",async(Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)!=null);return await db.Scoped<Benefit>(c).Where(x=>Hr(c)||x.EmployeeId==Employee(c)).ToListAsync();});
 api.MapPost("/payroll/benefits",async(BenefitInput i,Db db,HttpContext c)=>{Need(Hr(c));await Access.Internal(db,c,"payroll");await FindEmployee(db,c,i.EmployeeId);Require(!string.IsNullOrWhiteSpace(i.Name)&&i.Employer>=0&&i.EmployeeCost>=0,"Invalid benefit.");var row=new Benefit{TenantId=Tenant(c),EmployeeId=i.EmployeeId,Name=i.Name,Employer=i.Employer,EmployeeCost=i.EmployeeCost};db.Add(row);Log(db,c,"BenefitCreated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapGet("/payroll/runs",async(Db db,HttpContext c)=>{Need(Hr(c));return await db.Scoped<PayrollRun>(c).ToListAsync();});
 api.MapPost("/payroll/runs",async(RunInput i,Db db,HttpContext c)=>{Need(Hr(c));await Access.Internal(db,c,"payroll");await using var tx=await db.Database.BeginTransactionAsync();var rows=await Inputs(db,c,i.Period);var run=await db.Scoped<PayrollRun>(c).SingleOrDefaultAsync(x=>x.Period==i.Period);if(run==null){run=new PayrollRun{TenantId=Tenant(c),Period=i.Period};db.Add(run);}Require(run.Status=="Draft","Reviewed run is locked.");run.Snapshot=JsonSerializer.Serialize(rows);Log(db,c,"PayrollPrepared",run.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(run);});
 api.MapPost("/payroll/runs/{id:guid}/transition",async(Guid id,Decision i,Db db,HttpContext c)=>{
 Need(Hr(c));await Access.Internal(db,c,"payroll");await using var tx=await db.Database.BeginTransactionAsync();var run=await db.Scoped<PayrollRun>(c).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Run not found",404);Require(!string.IsNullOrWhiteSpace(i.Note),"Reason required.");
 Require((run.Status,i.Status) is ("Draft","Reviewed") or ("Reviewed","Approved") or ("Reviewed","Draft"),"Invalid transition.");
 if(i.Status!="Draft"){var live=await Inputs(db,c,run.Period);Need(live.All(x=>x.Pending==0)&&JsonSerializer.Serialize(live)==run.Snapshot,"Resolve pending or changed inputs; regenerate draft.",409);}
 if(i.Status=="Reviewed")run.ReviewedBy=Actor(c);
 if(i.Status=="Approved"){Need(run.ReviewedBy!=Actor(c),"Separate reviewer and approver required.");run.ApprovedBy=Actor(c);}
 if(i.Status=="Draft"){run.ReviewedBy=null;run.ApprovedBy=null;}run.Status=i.Status;Log(db,c,"Payroll"+i.Status,id,i.Note);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(run);
 });
 api.MapGet("/payroll/runs/{id:guid}/export",async(Guid id,Db db,HttpContext c)=>{Need(Hr(c));var run=await db.Scoped<PayrollRun>(c).SingleOrDefaultAsync(x=>x.Id==id&&x.Status=="Approved")??throw new RuleException("Approved run not found",404);return Results.Content(run.Snapshot,"application/json");});
 }
}
