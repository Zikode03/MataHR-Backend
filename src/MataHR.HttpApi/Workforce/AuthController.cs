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
public class AuthController(WorkforceAuthenticationService authentication, AccountSecurityService security, Volo.Abp.Authorization.Permissions.IPermissionChecker permissionChecker) : AbpController
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var account = await authentication.LoginAsync(input);
        if (account == null)
            return Unauthorized();
        if (account.RequiresTwoFactor)
        {
            var pending = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim("tenant", account.TenantId.ToString()), new Claim("security-stamp", account.SecurityStamp) }, "MataHR.Mfa");
            await HttpContext.SignInAsync("MataHR.Mfa", new ClaimsPrincipal(pending));
            return Accepted(new { requiresTwoFactor = true });
        }

        await SignIn(account);
        return Ok(account);
    }

    async Task SignIn(AuthAccountDto account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Email),
            new("tenant", account.TenantId.ToString()),
            new(Volo.Abp.Security.Claims.AbpClaimTypes.TenantId, account.TenantId.ToString()),
            new("role-set", string.Join(',', account.Roles)),
            new("security-stamp", account.SecurityStamp)
        };
        if (account.EmployeeId.HasValue)
            claims.Add(new("employee", account.EmployeeId.Value.ToString()));
        claims.AddRange(account.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    [AllowAnonymous, HttpPost("mfa/login"), EnableRateLimiting("login")]
    public async Task<IActionResult> CompleteMfa(MfaLoginInput input)
    {
        var pending = await HttpContext.AuthenticateAsync("MataHR.Mfa");
        if (!pending.Succeeded || !Guid.TryParse(pending.Principal?.FindFirst("tenant")?.Value, out var tenant) || !Guid.TryParse(pending.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var user))
            return Unauthorized();
        var account = await authentication.CompleteMfaAsync(tenant, user, pending.Principal!.FindFirst("security-stamp")!.Value, input.Code, input.RecoveryCode);
        if (account == null)
            return Unauthorized();
        await HttpContext.SignOutAsync("MataHR.Mfa");
        await SignIn(account);
        return Ok(account);
    }

    [Authorize, HttpPost("mfa/setup")]
    public async Task<MfaSetupDto> SetupMfa(MfaSetupInput input)
    {
        var setup = await security.SetupMfaAsync(input);
        await SignIn(await authentication.RefreshSessionAsync(CurrentTenant.Id!.Value, CurrentUser.Id!.Value));
        return setup;
    }

    [Authorize, HttpPost("mfa/enable")]
    public Task<RecoveryCodesDto> EnableMfa(MfaEnableInput input) => security.EnableMfaAsync(input);
    [Authorize, HttpPost("mfa/disable")]
    public Task DisableMfa(MfaDisableInput input) => security.DisableMfaAsync(input);
    [Authorize, HttpPost("password")]
    public Task ChangePassword(PasswordChangeInput input) => security.ChangePasswordAsync(input);
    [AllowAnonymous, HttpPost("password/request-reset"), EnableRateLimiting("login")]
    public async Task<IActionResult> RequestReset(PasswordResetRequest input)
    {
        await security.RequestResetAsync(input);
        return Accepted();
    }

    [AllowAnonymous, HttpPost("password/reset"), EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(PasswordResetInput input)
    {
        await security.ResetPasswordAsync(input);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var permissions = new List<string>();
        foreach (var permission in WorkforcePermissions.All)
            if (await permissionChecker.IsGrantedAsync(permission))
                permissions.Add(permission);
        return Ok(new { id = CurrentUser.Id, tenantId = CurrentTenant.Id, employeeId = User.FindFirst("employee")?.Value, roles = CurrentUser.Roles, permissions });
    }

    [Authorize, HttpPost("privacy")]
    public async Task<IActionResult> Privacy()
    {
        await authentication.AcknowledgeAsync(CurrentUser.Id!.Value);
        return Ok(new { version = WorkforceAuthenticationService.PrivacyVersion });
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return NoContent();
    }
}
