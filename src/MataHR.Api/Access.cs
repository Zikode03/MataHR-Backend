using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace MataHR;
public sealed class RuleException(string message,int status=400):Exception(message){public int Status=>status;}
public static class Access{
 public const string PrivacyVersion="preview-1.0";
 public static Guid Tenant(HttpContext c)=>Guid.Parse(c.User.FindFirstValue("tenant")!);
 public static Guid Actor(HttpContext c)=>Guid.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 public static Guid? Employee(HttpContext c)=>Guid.TryParse(c.User.FindFirstValue("employee"),out var id)?id:null;
 public static bool Hr(HttpContext c)=>c.User.IsInRole("HR");
 public static bool Admin(HttpContext c)=>c.User.IsInRole("Admin");
 public static void Need(bool condition,string message="Forbidden",int status=403){if(!condition)throw new RuleException(message,status);}
 public static void Require(bool condition,string message)=>Need(condition,message,400);
 public static IQueryable<T> Scoped<T>(this Db db,HttpContext c) where T:TenantRow=>db.Set<T>().Where(x=>x.TenantId==Tenant(c));
 public static async Task<Employee> FindEmployee(Db db,HttpContext c,Guid id)=>await db.Scoped<Employee>(c).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Employee not found",404);
 public static async Task<bool> CanView(Db db,HttpContext c,Guid employee){if(Hr(c)||Employee(c)==employee)return true;return c.User.IsInRole("Manager")&&Employee(c) is Guid manager&&await db.Scoped<Employee>(c).AnyAsync(x=>x.Id==employee&&x.ManagerId==manager);}
 public static async Task<bool> CanReview(Db db,HttpContext c,Guid employee){if(Employee(c)==employee)return false;if(Hr(c))return true;return c.User.IsInRole("Manager")&&Employee(c) is Guid manager&&await db.Scoped<Employee>(c).AnyAsync(x=>x.Id==employee&&x.ManagerId==manager);}
 public static async Task<Guid[]> Visible(Db db,HttpContext c){var own=Employee(c);var manager=c.User.IsInRole("Manager");return await db.Scoped<Employee>(c).Where(x=>Hr(c)||x.Id==own||(manager&&own!=null&&x.ManagerId==own)).Select(x=>x.Id).ToArrayAsync();}
 public static void Log(Db db,HttpContext c,string action,Guid target,string note="")=>db.Add(new Audit{TenantId=Tenant(c),ActorId=Actor(c),Action=action,TargetId=target,Note=note});
 public static DateOnly Today()=>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2));
 public static bool Active(Employee e,DateOnly d)=>e.Started<=d&&(e.Exited==null||e.Exited>d);
 public static async Task Internal(Db db,HttpContext c,string kind){Need(!await db.Scoped<Integration>(c).AnyAsync(x=>x.Kind==kind&&x.Mode=="external"),"External source of truth; local changes disabled.",409);}
 public static async Task<int> Days(Db db,HttpContext c,Guid employee,DateOnly start,DateOnly end){
 Require(start<=end&&end.DayNumber-start.DayNumber<=366,"Invalid date range.");var schedule=await db.Scoped<Schedule>(c).SingleOrDefaultAsync(x=>x.EmployeeId==employee);var holidays=await db.Scoped<Holiday>(c).Where(x=>x.Date>=start&&x.Date<=end).Select(x=>x.Date).ToListAsync();int mask=schedule?.DaysMask??62;int n=0;for(var d=start;d<=end;d=d.AddDays(1))if((mask&(1<<(int)d.DayOfWeek))!=0&&!holidays.Contains(d))n++;return n;
 }
}
