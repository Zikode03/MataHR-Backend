using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;
namespace MataHR.HttpApi;

[ApiController, Route("api/auth")]
public class AuthController(WorkforceAuthenticationService authentication) : AbpController
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var account = await authentication.LoginAsync(input);
        if (account == null)
            return Unauthorized();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, account.Id.ToString()), new(ClaimTypes.Name, account.Email), new("tenant", account.TenantId.ToString()), new(Volo.Abp.Security.Claims.AbpClaimTypes.TenantId, account.TenantId.ToString()), new("role-set", string.Join(',', account.Roles)) };
        if (account.EmployeeId.HasValue)
            claims.Add(new("employee", account.EmployeeId.Value.ToString()));
        claims.AddRange(account.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Ok(account);
    }
    [Authorize, HttpGet("me")] public IActionResult Me() => Ok(new { id = CurrentUser.Id, tenantId = CurrentTenant.Id, employeeId = User.FindFirst("employee")?.Value, roles = CurrentUser.Roles, permissions = WorkforcePermissions.All.Where(x => WorkforcePermissions.Allows(CurrentUser.Roles, x)).ToArray() });
    [Authorize, HttpPost("privacy")]
    public async Task<IActionResult> Privacy()
    {
        await authentication.AcknowledgeAsync(CurrentUser.Id!.Value);
        return Ok(new
        {
            version = WorkforceAuthenticationService.PrivacyVersion
        });
    }
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return NoContent();
    }
}
