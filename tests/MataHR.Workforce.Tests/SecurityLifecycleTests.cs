using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using MataHR.Workforce.Contracts;
using Xunit;

namespace MataHR.Workforce.Tests;
public class SecurityLifecycleTests(WorkforceFixture f) : IClassFixture<WorkforceFixture>
{
    async Task<string> Provision()
    {
        using var admin = await f.Client("admin");
        var email = "security-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        var response = await admin.PostAsJsonAsync("/api/admin/accounts", new AccountInput(email, WorkforceFixture.Password, ["Employee"], f.Outsider.Id));
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + response.RequestMessage?.RequestUri + " " + (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        return email;
    }

    async Task<HttpClient> Login(string email, string password = WorkforceFixture.Password)
    {
        var c = f.NewClient();
        var response = await c.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return c;
    }

    [Fact]
    public async Task PasswordChangeRevokesExistingCookie()
    {
        var email = await Provision();
        using var c = await Login(email);
        const string changed = "Changed-Test-Passphrase42!";
        var response = await c.PostAsJsonAsync("/api/auth/password", new PasswordChangeInput(WorkforceFixture.Password, changed));
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + response.RequestMessage?.RequestUri + " " + (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        using var fresh = f.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, WorkforceFixture.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, changed))).StatusCode);
    }

    [Fact]
    public async Task ResetTokensAreOneUseAndRevokeTheOldSession()
    {
        var email = await Provision();
        using var old = await Login(email);
        using var anon = f.NewClient();
        var response = await anon.PostAsJsonAsync("/api/auth/password/request-reset", new PasswordResetRequest(f.TenantA, email));
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + response.RequestMessage?.RequestUri + " " + (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var token = f.Recovery.Messages.Last(x => x.Email == email).Token;
        var payload = new PasswordResetInput(f.TenantA, email, token, "Reset-Test-Passphrase42!");
        Assert.Equal(HttpStatusCode.NoContent, (await anon.PostAsJsonAsync("/api/auth/password/reset", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/auth/me")).StatusCode);
        Assert.False((await anon.PostAsJsonAsync("/api/auth/password/reset", payload)).IsSuccessStatusCode);
        using var reset = await Login(email, payload.NewPassword);
        Assert.Equal(HttpStatusCode.OK, (await reset.GetAsync("/api/auth/me")).StatusCode);
        var before = f.Recovery.Messages.Count;
        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/api/auth/password/request-reset", new PasswordResetRequest(f.TenantA, "absent@example.invalid"))).StatusCode);
        Assert.Equal(before, f.Recovery.Messages.Count);
    }

    [Fact]
    public async Task MfaRequiresChallengeAndRecoveryCodeCannotBeReplayed()
    {
        var email = await Provision();
        using var current = await Login(email);
        var response = await current.PostAsJsonAsync("/api/auth/mfa/setup", new MfaSetupInput(WorkforceFixture.Password));
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + response.RequestMessage?.RequestUri + " " + (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var setup = (await response.Content.ReadFromJsonAsync<MfaSetupDto>())!;
        response = await current.PostAsJsonAsync("/api/auth/mfa/enable", new MfaEnableInput(Totp(setup.SharedKey)));
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + response.RequestMessage?.RequestUri + " " + (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        var recovery = (await response.Content.ReadFromJsonAsync<RecoveryCodesDto>())!;
        Assert.NotEmpty(recovery.Codes);
        Assert.Equal(HttpStatusCode.Unauthorized, (await current.GetAsync("/api/auth/me")).StatusCode);
        using var pending = f.NewClient();
        Assert.Equal(HttpStatusCode.Accepted, (await pending.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, WorkforceFixture.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await pending.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await pending.PostAsJsonAsync("/api/auth/mfa/login", new MfaLoginInput("invalid"))).StatusCode);
        response = await pending.PostAsJsonAsync("/api/auth/mfa/login", new MfaLoginInput(Totp(setup.SharedKey)));
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + response.RequestMessage?.RequestUri + " " + (await response.Content.ReadAsStringAsync()) + f.Logs.Text);
        Assert.Equal(HttpStatusCode.OK, (await pending.GetAsync("/api/auth/me")).StatusCode);
        await pending.PostAsJsonAsync("/api/auth/logout", new { });
        using var recover = f.NewClient();
        Assert.Equal(HttpStatusCode.Accepted, (await recover.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, WorkforceFixture.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recover.PostAsJsonAsync("/api/auth/mfa/login", new MfaLoginInput(recovery.Codes[0], true))).StatusCode);
        await recover.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.Accepted, (await recover.PostAsJsonAsync("/api/auth/login", new LoginInput(f.TenantA, email, WorkforceFixture.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await recover.PostAsJsonAsync("/api/auth/mfa/login", new MfaLoginInput(recovery.Codes[0], true))).StatusCode);
    }

    [Fact]
    public async Task PersistedAuditParametersDoNotContainPasswords()
    {
        await Provision();
        await using var sql = new SqlConnection(f.Connection);
        await sql.OpenAsync();
        await using var query = sql.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM AbpAuditLogs";
        Assert.True(Convert.ToInt32(await query.ExecuteScalarAsync()) > 0);
        query.CommandText = "SELECT COUNT(*) FROM AbpAuditLogActions WHERE Parameters LIKE @secret";
        query.Parameters.AddWithValue("@secret", "%" + WorkforceFixture.Password + "%");
        Assert.Equal(0, Convert.ToInt32(await query.ExecuteScalarAsync()));
        query.CommandText = "SELECT COUNT(*) FROM AbpEntityPropertyChanges WHERE PropertyName IN ('PasswordHash','Content','Snapshot')";
        Assert.Equal(0, Convert.ToInt32(await query.ExecuteScalarAsync()));
    }

    static string Totp(string secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var ch in secret.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(ch);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }

        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counter);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        int offset = hash[^1] & 15;
        int binary = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1000000).ToString("D6");
    }
}
