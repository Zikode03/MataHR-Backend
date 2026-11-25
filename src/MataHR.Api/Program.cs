using MataHR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;
var b=WebApplication.CreateBuilder(args);
var connection=b.Configuration.GetConnectionString("Default");
if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Configure ConnectionStrings__Default for Microsoft SQL Server.");
b.Services.AddDbContext<Db>(o=>o.UseSqlServer(connection));
b.Services.AddScoped<IPasswordHasher<Account>,PasswordHasher<Account>>();
b.Services.AddOpenApi(); b.Services.AddProblemDetails();
b.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
 o.Cookie.Name="matahr-session";o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;
 o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
 o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
 o.Events.OnValidatePrincipal=async c=>{var db=c.HttpContext.RequestServices.GetRequiredService<Db>();var id=c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);var tenant=c.Principal?.FindFirstValue("tenant");
 if(!Guid.TryParse(id,out var a)||!Guid.TryParse(tenant,out var t)){c.RejectPrincipal();return;}
 var account=await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==a&&x.TenantId==t&&x.Active);
 if(account is null||account.Roles!=c.Principal!.FindFirstValue("role-set")||account.EmployeeId?.ToString()!=(c.Principal.FindFirstValue("employee"))){c.RejectPrincipal();}
 };
});
b.Services.AddAuthorization();b.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
await b.AddApplicationAsync<MataHrHostModule>();
var app=b.Build();
await app.InitializeApplicationAsync();app.UseExceptionHandler();
app.Use(async(ctx,next)=>{if(!HttpMethods.IsGet(ctx.Request.Method)&&!HttpMethods.IsHead(ctx.Request.Method)&&!HttpMethods.IsOptions(ctx.Request.Method)&&ctx.Request.Headers["X-MataHR-Request"]!="1"){ctx.Response.StatusCode=400;return;}await next();});
app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
if(app.Environment.IsDevelopment())app.MapOpenApi();
app.MapGet("/health",()=>Results.Ok(new{status="ok"}));
using(var scope=app.Services.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<Db>();
 if(!app.Environment.IsDevelopment()&&!app.Environment.IsEnvironment("Testing"))throw new InvalidOperationException("Production deployment is disabled until reviewed migrations and operational controls are configured.");
 if(b.Configuration.GetValue<bool>("Database:InitializeDevelopmentSchema")||app.Environment.IsEnvironment("Testing"))await db.Database.EnsureCreatedAsync();
 else if(!await db.Database.CanConnectAsync())throw new InvalidOperationException("Database unavailable. Configure SQL Server and initialize the development schema explicitly.");
 var email=b.Configuration["Bootstrap:Email"];var password=b.Configuration["Bootstrap:Password"];
 if(!string.IsNullOrWhiteSpace(email)&&!string.IsNullOrWhiteSpace(password)){
 if(password.Length<14)throw new InvalidOperationException("Bootstrap password must be at least 14 characters.");
 if(!await db.Accounts.AnyAsync()){var tenant=new Tenant{Id=Guid.NewGuid(),Name=b.Configuration["Bootstrap:Company"]??"Matateni Technologies"};var user=new Account{TenantId=tenant.Id,Email=email.Trim().ToLowerInvariant(),Roles="Admin"};user.PasswordHash=scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>().HashPassword(user,password);db.AddRange(tenant,user);await db.SaveChangesAsync();}
 }
}
app.MapPost("/api/auth/login",async(LoginInput input,Db db,IPasswordHasher<Account> hasher,HttpContext ctx)=>{
 var email=input.Email?.Trim().ToLowerInvariant();var a=await db.Accounts.SingleOrDefaultAsync(x=>x.TenantId==input.TenantId&&x.Email==email&&x.Active);
 if(a is null||hasher.VerifyHashedPassword(a,a.PasswordHash,input.Password??"")==PasswordVerificationResult.Failed)return Results.Unauthorized();
 var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,a.Id.ToString()),new("tenant",a.TenantId.ToString()),new("role-set",a.Roles),new(ClaimTypes.Name,a.Email)};
 if(a.EmployeeId is Guid employee)claims.Add(new("employee",employee.ToString()));
 claims.AddRange(a.Roles.Split(',').Select(r=>new Claim(ClaimTypes.Role,r)));
 await ctx.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
 return Results.Ok(new{a.Id,a.Email,a.TenantId,a.EmployeeId,roles=a.Roles.Split(',')});
}).RequireRateLimiting("login");
app.MapPost("/api/auth/logout",async(HttpContext c)=>{await c.SignOutAsync();return Results.NoContent();}).RequireAuthorization();
app.MapGet("/api/auth/me",(HttpContext c)=>Results.Ok(new{id=Access.Actor(c),tenantId=Access.Tenant(c),employeeId=Access.Employee(c),roles=c.User.FindAll(ClaimTypes.Role).Select(x=>x.Value)})).RequireAuthorization();
app.MapPost("/api/auth/privacy",async(HttpContext c,Db db)=>{var id=Access.Actor(c);var t=Access.Tenant(c);if(!await db.PrivacyAcks.AnyAsync(x=>x.TenantId==t&&x.AccountId==id&&x.Version==Access.PrivacyVersion)){db.Add(new PrivacyAck{TenantId=t,AccountId=id,Version=Access.PrivacyVersion});await db.SaveChangesAsync();}return Results.Ok(new{version=Access.PrivacyVersion});}).RequireAuthorization();
var api=app.MapGroup("/api").RequireAuthorization().AddEndpointFilter(async(context,next)=>{
 var c=context.HttpContext;var db=c.RequestServices.GetRequiredService<Db>();var t=Access.Tenant(c);var a=Access.Actor(c);
 if(!await db.PrivacyAcks.AnyAsync(x=>x.TenantId==t&&x.AccountId==a&&x.Version==Access.PrivacyVersion))return Results.Json(new{error="Privacy acknowledgement required."},statusCode:403);
 try{return await next(context);}catch(RuleException e){return Results.Problem(e.Message,statusCode:e.Status);}catch(DbUpdateException){return Results.Conflict(new{error="Concurrent or duplicate update. Refresh and retry."});}
});
api.MapOrganisation();api.MapPeople();api.MapAttendance();api.MapLeave();api.MapPayroll();
app.Run();
public partial class Program {}
public record LoginInput(Guid TenantId,string? Email,string? Password);
