using Microsoft.EntityFrameworkCore;
using MataHR.Contracts;
using MataHR.Foundation;
using static MataHR.Access;
namespace MataHR;
public static class OrganisationApi {
 static void Write(HttpContext c)=>Need(Permissions.Allows(c.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(x=>x.Value),Permissions.OrganisationWrite));
 public static void MapOrganisation(this RouteGroupBuilder api){
 api.MapGet("/organisation/structure",async(Db db,HttpContext c)=>{
 Need(Hr(c)||Employee(c)!=null);var ids=await Visible(db,c);
 var branches=await db.Scoped<Branch>(c).ToListAsync();var departments=await db.Scoped<Department>(c).ToListAsync();var positions=await db.Scoped<Position>(c).ToListAsync();
 var people=await db.Scoped<Employee>(c).Where(x=>ids.Contains(x.Id)).ToListAsync();var assignments=await db.Scoped<Assignment>(c).Where(x=>ids.Contains(x.EmployeeId)).ToListAsync();
 return Results.Ok(new{branches,departments,positions,assignments,employees=people.Select(x=>new{x.Id,x.Number,x.Name,x.ManagerId,x.Position,x.Department})});
 });
 api.MapPost("/organisation/branches",async(BranchInput i,Db db,HttpContext c)=>{Write(c);Require(!string.IsNullOrWhiteSpace(i.Name),"Branch name required.");var row=new Branch{TenantId=Tenant(c),Name=i.Name.Trim()};db.Add(row);Log(db,c,"BranchCreated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapPost("/organisation/branch-departments",async(MataHR.Contracts.DepartmentInput i,Db db,HttpContext c)=>{Write(c);Require(!string.IsNullOrWhiteSpace(i.Name)&&await db.Scoped<Branch>(c).AnyAsync(x=>x.Id==i.BranchId),"Valid branch and department required.");var row=new Department{TenantId=Tenant(c),BranchId=i.BranchId,Name=i.Name.Trim()};db.Add(row);Log(db,c,"DepartmentCreated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapPost("/organisation/positions",async(PositionInput i,Db db,HttpContext c)=>{Write(c);Require(!string.IsNullOrWhiteSpace(i.Name)&&await db.Scoped<Department>(c).AnyAsync(x=>x.Id==i.DepartmentId),"Valid department and position required.");var row=new Position{TenantId=Tenant(c),DepartmentId=i.DepartmentId,Name=i.Name.Trim()};db.Add(row);Log(db,c,"PositionCreated",row.Id);await db.SaveChangesAsync();return Results.Ok(row);});
 api.MapPut("/organisation/assignments",async(OrganisationAssignmentInput i,Db db,HttpContext c,IOrganisationRules rules)=>{
 Write(c);await using var tx=await db.Database.BeginTransactionAsync();var employee=await FindEmployee(db,c,i.EmployeeId);Require(Active(employee,Today()),"Current employee required.");var pos=await db.Scoped<Position>(c).SingleOrDefaultAsync(x=>x.Id==i.PositionId)??throw new RuleException("Position not found",404);
 var dep=await db.Scoped<Department>(c).SingleAsync(x=>x.Id==pos.DepartmentId);
 var links=await db.Scoped<Employee>(c).ToDictionaryAsync(x=>x.Id,x=>x.ManagerId);if(i.ManagerId is Guid manager){var mgr=await FindEmployee(db,c,manager);Require(Active(mgr,Today())&&await db.Scoped<Assignment>(c).AnyAsync(x=>x.EmployeeId==manager),"Manager must have current chart placement.");}
 try{rules.ValidateAssignment(employee.Id,i.ManagerId,links);}catch(DomainRuleException e){throw new RuleException(e.Message);}
 var row=await db.Scoped<Assignment>(c).SingleOrDefaultAsync(x=>x.EmployeeId==employee.Id);if(row==null){row=new Assignment{TenantId=Tenant(c),EmployeeId=employee.Id};db.Add(row);}row.PositionId=pos.Id;row.ManagerId=i.ManagerId;
 employee.Position=pos.Name;employee.Department=dep.Name;employee.ManagerId=i.ManagerId;
 db.Add(new EmploymentEvent{TenantId=Tenant(c),EmployeeId=employee.Id,Description="Organisation assignment updated"});Log(db,c,"AssignmentUpdated",row.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
 });
 }
}
