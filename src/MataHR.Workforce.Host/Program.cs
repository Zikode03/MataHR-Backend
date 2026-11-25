using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MataHR.Persistence;
using MataHR.Workforce;
using MataHR.Workforce.Host;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
var b = WebApplication.CreateBuilder(args);
b.Host.UseAutofac();
if (b.Environment.IsDevelopment() && b.Configuration.GetValue<bool>("Database:UseManagedNetworkingOnWindows")) AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

b.Services.AddControllers().AddApplicationPart(typeof(MataHR.HttpApi.EmployeesController).Assembly);
b.Services.AddOpenApi();
b.Services.AddProblemDetails();
b.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "matahr-workforce-session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        if (!Guid.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || !Guid.TryParse(c.Principal?.FindFirstValue("tenant"), out var tenant))
        {
            c.RejectPrincipal();
            return;
        }
        var db = c.HttpContext.RequestServices.GetRequiredService<WorkforceDbContext>();
        var account = await db.Set<WorkforceUser>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant && x.Active);
        if (account == null || account.Roles != c.Principal!.FindFirstValue("role-set") || account.EmployeeId?.ToString() != c.Principal.FindFirstValue("employee"))
            c.RejectPrincipal();
    };
});
b.Services.AddAuthorization();
b.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
await b.AddApplicationAsync<WorkforceHostModule>();
var app = b.Build();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default"))) throw new InvalidOperationException("Configure ConnectionStrings__Default for Microsoft SQL Server.");
await app.InitializeApplicationAsync();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) throw new InvalidOperationException("Production deployment requires operational security and deployment review.");
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
    if (b.Configuration.GetValue<bool>("Database:InitializeDevelopmentSchema") || app.Environment.IsEnvironment("Testing"))
        await db.Database.MigrateAsync();
    else if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply WorkforceDbContext migrations before starting the backend.");
    var email = b.Configuration["Bootstrap:Email"];
    var password = b.Configuration["Bootstrap:Password"];
    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        if (password.Length < 14)
            throw new InvalidOperationException("Bootstrap account password must be at least 14 characters.");
        var requested = b.Configuration["Bootstrap:TenantId"];
        var tenant = Guid.TryParse(requested, out var configured) ? configured : Guid.NewGuid();
        if (!await db.Set<WorkforceUser>().IgnoreQueryFilters().AnyAsync())
        {
            using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant))
            {
                var account = new WorkforceUser { TenantId = tenant, Email = email.Trim().ToLowerInvariant(), Roles = "Admin" };
                account.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<WorkforceUser>>().HashPassword(account, password);
                db.Add(account);
                await db.SaveChangesAsync();
            }
        }
    }
}
app.UseExceptionHandler();
app.Use(async (ctx, next) => { if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method) && ctx.Request.Headers["X-MataHR-Request"] != "1") { ctx.Response.StatusCode = 400; return; } await next(); });
app.UseRouting();
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true)
    {
        if (!Guid.TryParse(ctx.User.FindFirstValue("tenant"), out var tenant))
        {
            ctx.Response.StatusCode = 401;
            return;
        }
        using (ctx.RequestServices.GetRequiredService<ICurrentTenant>().Change(tenant))
        {
            if (ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/api/auth"))
            {
                var account = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var db = ctx.RequestServices.GetRequiredService<WorkforceDbContext>();
                if (!await db.Set<PrivacyAcceptance>().AnyAsync(x => x.AccountId == account && x.Version == WorkforceAuthenticationService.PrivacyVersion))
                {
                    ctx.Response.StatusCode = 403;
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        error = "Privacy acknowledgement required."
                    });
                    return;
                }
            }
            await next();
        }
    }
    else
        await next();
});
app.UseAuthorization();
app.UseRateLimiter();
app.UseUnitOfWork();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
public partial class WorkforceProgram
{
}
