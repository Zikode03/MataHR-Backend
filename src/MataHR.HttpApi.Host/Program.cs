using MataHR.Authentication;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MataHR.Persistence;
using MataHR.HttpApi.Host;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Data;
using Volo.Abp.Identity;

var b = WebApplication.CreateBuilder(args);

// The local launch profile selects the clean database while retaining the connection credentials from user secrets.
if (b.Environment.IsDevelopment()
    && b.Configuration["Database:DevelopmentCatalog"] is { Length: > 0 } catalog
    && b.Configuration.GetConnectionString("Default") is { Length: > 0 } configuredConnection)
{
    var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = catalog };
    b.Configuration["ConnectionStrings:Default"] = connection.ConnectionString;
}

b.Host.UseAutofac();
if (b.Environment.IsDevelopment() && b.Configuration.GetValue<bool>("Database:UseManagedNetworkingOnWindows"))
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
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
    o.Events.OnRedirectToLogin = c =>
    {
        c.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = c =>
    {
        c.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
    o.Events.OnValidatePrincipal = async c =>
    {
        if (!Guid.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || !Guid.TryParse(c.Principal?.FindFirstValue("tenant"), out var tenant))
        {
            c.RejectPrincipal();
            return;
        }

        var db = c.HttpContext.RequestServices.GetRequiredService<MataHrDbContext>();
        var account = await db.Set<Volo.Abp.Identity.IdentityUser>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant && x.IsActive && !x.IsDeleted);
        string[] roleNames = [];
        using (c.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>().Change(tenant))
        {
            if (account != null)
                roleNames = (await c.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>().GetRolesAsync(account)).Order().ToArray();
        }

        if (account == null || string.Join(',', roleNames) != c.Principal!.FindFirstValue("role-set") || account.GetProperty<Guid?>("EmployeeId")?.ToString() != c.Principal.FindFirstValue("employee") || account.SecurityStamp != c.Principal.FindFirstValue("security-stamp"))
            c.RejectPrincipal();
    };
}).AddCookie("MataHR.Mfa", o =>
{
    o.Cookie.Name = "matahr-mfa-pending";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(4);
});
b.Services.AddAuthorization();
b.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("clock", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
await b.AddApplicationAsync<WorkforceHostModule>();
b.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});
var app = b.Build();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
    throw new InvalidOperationException("Configure ConnectionStrings__Default for Microsoft SQL Server.");
await app.InitializeApplicationAsync();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("Production deployment requires operational security and deployment review.");
// Database setup is prepared by MataHR.DbMigrator before the API starts.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MataHrDbContext>();
    if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Database setup is incomplete. Run scripts/Migrate-Workforce.ps1, then start the API again.");
}

app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method) && ctx.Request.Headers["X-MataHR-Request"] != "1")
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    await next();
});
app.UseRouting();
// Establish the authenticated identity before deciding which tenant and workspace the request can use.
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
                var db = ctx.RequestServices.GetRequiredService<MataHrDbContext>();
                var accountRow = await db.Set<Volo.Abp.Identity.IdentityUser>().SingleAsync(x => x.Id == account);
                if (accountRow.GetProperty<bool>("RequirePasswordChange"))
                {
                    ctx.Response.StatusCode = 403;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Change your temporary password before opening your workspace." });
                    return;
                }

                if (!await db.Set<PrivacyAcceptance>().AnyAsync(x => x.AccountId == account && x.Version == WorkforceAuthenticationService.PrivacyVersion))
                {
                    ctx.Response.StatusCode = 403;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Privacy acknowledgement required." });
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
app.UseAuditing();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "MataHR API v1");
        options.DocumentTitle = "MataHR API Explorer";
        options.EnableFilter();
        options.DisplayRequestDuration();
        options.UseRequestInterceptor("(request) => { request.headers['X-MataHR-Request'] = '1'; request.credentials = 'same-origin'; return request; }");
    });
}

app.MapGet("/api/auth/antiforgery", (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions> options) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken, headerName = options.Value.HeaderName });
}).AllowAnonymous();
app.MapGet("/api/auth/config", () => Results.Ok(new { tenantId = b.Configuration["Bootstrap:TenantId"] })).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
public partial class WorkforceProgram
{
}
