using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using static MataHR.Access;
namespace MataHR;
public record AccountInput(string Email,string Password,string[] Roles,Guid? EmployeeId);
public record EmployeeInput(string Number,string Name,string Email,string Department,string Position,string ContractType,DateOnly Started,DateOnly? Exited,Guid? ManagerId);
public record DepartmentInput(string Name);
public record ContractInput(Guid EmployeeId,string Reference,DateOnly Start,DateOnly? End);
public record IntegrationInput(string Mode,string Provider,string Reference);
public static class PeopleApi{
 public static void MapPeople(this RouteGroupBuilder api){
 api.MapGet("/admin/accounts",async(Db db,HttpContext c)=>{Need(Admin(c));return await db.Scoped<Account>(c).ToListAsync();});
 api.MapPost("/admin/accounts",async(AccountInput i,Db db,HttpContext c,IPasswordHasher<Account> hasher)=>{
 Need(Admin(c));Require(!string.IsNullOrWhiteSpace(i.Email)&&i.Password?.Length>=14&&i.Roles is {Length:>0}&&i.Roles.All(r=>new[]{"Admin","HR","Manager","Employee"}.Contains(r)),"Valid email, roles and password (14+ characters) required.");
 if(i.EmployeeId is Guid e)await FindEmployee(db,c,e);
 Require(!i.Roles.Any(r=>r=="Employee"||r=="Manager")||i.EmployeeId!=null,"Employee or manager requires an employee record.");
 var a=new Account{TenantId=Tenant(c),Email=i.Email.Trim().ToLowerInvariant(),Roles=string.Join(',',i.Roles.Distinct()),EmployeeId=i.EmployeeId};a.PasswordHash=hasher.HashPassword(a,i.Password!);db.Add(a);Log(db,c,"AccountCreated",a.Id);await db.SaveChangesAsync();return Results.Created("/api/admin/accounts/"+a.Id,a);
 });
 api.MapGet("/admin/audit",async(Db db,HttpContext c)=>{Need(Admin(c));return await db.Scoped<Audit>(c).OrderByDescending(x=>x.Id).Take(500).Select(x=>new{x.Id,x.ActorId,x.Action,x.TargetId,x.At}).ToListAsync();});
 api.MapGet("/employees",async(Db db,HttpContext c)=>{var ids=await Visible(db,c);return await db.Scoped<Employee>(c).Where(x=>ids.Contains(x.Id)).ToListAsync();});
 api.MapPost("/employees",async(EmployeeInput i,Db db,HttpContext c)=>await SaveEmployee(i,null,db,c));
 api.MapPut("/employees/{id:guid}",async(Guid id,EmployeeInput i,Db db,HttpContext c)=>await SaveEmployee(i,id,db,c));
 api.MapGet("/employees/{id:guid}/history",async(Guid id,Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)==id);return await db.Scoped<EmploymentEvent>(c).Where(x=>x.EmployeeId==id).ToListAsync();});
 api.MapGet("/organisation/departments",async(Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)!=null);return await db.Scoped<Department>(c).ToListAsync();});
 api.MapPost("/organisation/departments",async(DepartmentInput i,Db db,HttpContext c)=>{Need(Hr(c));Require(!string.IsNullOrWhiteSpace(i.Name),"Department name required.");var d=new Department{TenantId=Tenant(c),Name=i.Name.Trim()};db.Add(d);Log(db,c,"DepartmentCreated",d.Id);await db.SaveChangesAsync();return Results.Ok(d);});
 api.MapGet("/employees/{id:guid}/contracts",async(Guid id,Db db,HttpContext c)=>{Need(Hr(c)||Employee(c)==id);return await db.Scoped<Contract>(c).Where(x=>x.EmployeeId==id).ToListAsync();});
 api.MapPost("/contracts",async(ContractInput i,Db db,HttpContext c)=>{Need(Hr(c));await FindEmployee(db,c,i.EmployeeId);Require(!string.IsNullOrWhiteSpace(i.Reference)&&(i.End==null||i.End>=i.Start),"Invalid contract.");var row=new Contract{TenantId=Tenant(c),EmployeeId=i.EmployeeId,Reference=i.Reference,Start=i.Start,End=i.End};db.Add(row);Log(db,c,"ContractCreated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapGet("/integrations",async(Db db,HttpContext c)=>{Need(Admin(c)||Hr(c));return await db.Scoped<Integration>(c).ToListAsync();});
 api.MapPut("/integrations/{kind}",async(string kind,IntegrationInput i,Db db,HttpContext c)=>{Need(Admin(c)||Hr(c));Require(new[]{"leave","payroll"}.Contains(kind)&&new[]{"internal","external"}.Contains(i.Mode)&&(i.Mode!="external"||!string.IsNullOrWhiteSpace(i.Provider)),"Invalid integration configuration.");var row=await db.Scoped<Integration>(c).SingleOrDefaultAsync(x=>x.Kind==kind);if(row==null){row=new Integration{TenantId=Tenant(c),Kind=kind};db.Add(row);}row.Mode=i.Mode;row.Provider=i.Provider??"";row.Reference=i.Reference??"";Log(db,c,"IntegrationConfigured",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapGet("/dashboard",async(Db db,HttpContext c)=>{var ids=await Visible(db,c);var today=Today();return Results.Ok(new{employees=await db.Scoped<Employee>(c).CountAsync(x=>ids.Contains(x.Id)&&x.Started<=today&&(x.Exited==null||x.Exited>today)),recorded=await db.Scoped<Session>(c).Where(x=>ids.Contains(x.EmployeeId)&&x.Date==today&&x.Status!="Rejected").Select(x=>x.EmployeeId).Distinct().CountAsync(),onLeave=await db.Scoped<LeaveRequest>(c).Where(x=>ids.Contains(x.EmployeeId)&&x.Status=="Approved"&&x.Start<=today&&x.End>=today).Select(x=>x.EmployeeId).Distinct().CountAsync(),pendingLeave=await db.Scoped<LeaveRequest>(c).CountAsync(x=>ids.Contains(x.EmployeeId)&&x.Status=="Pending"),pendingAttendance=await db.Scoped<Session>(c).CountAsync(x=>ids.Contains(x.EmployeeId)&&x.Status=="Pending review")});});
 }
 static async Task<IResult> SaveEmployee(EmployeeInput i,Guid? id,Db db,HttpContext c){
 Need(Hr(c));Require(!string.IsNullOrWhiteSpace(i.Number)&&!string.IsNullOrWhiteSpace(i.Name)&&!string.IsNullOrWhiteSpace(i.Department)&&i.Started!=default&&(i.Exited==null||i.Exited>=i.Started),"Invalid employee fields.");
 await using var tx=await db.Database.BeginTransactionAsync();
 var row=id is Guid e?await FindEmployee(db,c,e):new Employee{TenantId=Tenant(c)};
 var seen=new HashSet<Guid>{row.Id};var manager=i.ManagerId;while(manager is Guid m){Require(seen.Add(m),"Circular reporting relationship.");manager=(await FindEmployee(db,c,m)).ManagerId;}
 if(id==null)db.Add(row);
 row.Number=i.Number.Trim();row.Name=i.Name.Trim();row.Email=i.Email??"";row.Department=i.Department;row.Position=i.Position??"";row.ContractType=i.ContractType??"";row.Started=i.Started;row.Exited=i.Exited;row.ManagerId=i.ManagerId;
 db.Add(new EmploymentEvent{TenantId=Tenant(c),EmployeeId=row.Id,Description=id==null?"Employee created":"Employment record updated"});Log(db,c,id==null?"EmployeeCreated":"EmployeeUpdated",row.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
 }
}
