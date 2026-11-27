using MataHR.Authentication.Contracts;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MataHR.IntegrationTests;

// Exercise repeated sign-in failures through HTTP so the test covers the lockout configuration as well as the identity logic.
public class LockoutTests(MataHrApiFixture f) : IClassFixture<MataHrApiFixture>
{
    [Fact]
    public async Task RepeatedInvalidCredentialsLockAccount()
    {
        using var c = f.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-MataHR-Request", "1");
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, "lockout@example.invalid", "Wrong-test-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, "lockout@example.invalid", MataHrApiFixture.Password))).StatusCode);
    }
}
