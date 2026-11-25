using System.Net;
using System.Net.Http.Json;
using MataHR.Workforce.Contracts;
using Xunit;

namespace MataHR.Workforce.Tests;
public class LockoutTests(WorkforceFixture f) : IClassFixture<WorkforceFixture>
{
    [Fact]
    public async Task RepeatedInvalidCredentialsLockAccount()
    {
        using var c = f.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-MataHR-Request", "1");
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, "lockout@example.invalid", "Wrong-test-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, "lockout@example.invalid", WorkforceFixture.Password))).StatusCode);
    }
}
