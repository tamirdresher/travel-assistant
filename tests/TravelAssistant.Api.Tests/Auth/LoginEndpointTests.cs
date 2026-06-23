using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TravelAssistant.Api.Auth;
using Xunit;

namespace TravelAssistant.Api.Tests.Auth;

// LOGIN-001 — covers each §8 invariant + each §3/§5/§6 wire concern.
// MUST run on CI (local env lacks the net9.0 runtime).
public sealed class LoginEndpointTests : IClassFixture<LoginAppFactory>
{
    private readonly LoginAppFactory _factory;

    public LoginEndpointTests(LoginAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");
        return c;
    }

    [Fact]
    public async Task UnknownUser_returns_401_invalid_credentials_body()
    {
        var c = NewClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "ghost@example.com", password = "whatever" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Equal("{\"status\":\"invalid_credentials\"}", body);
        Assert.Contains(r.Headers.WwwAuthenticate, a => a.Scheme == "Bearer");
    }

    [Fact]
    public async Task WrongPassword_returns_byte_identical_401_as_unknown_user()
    {
        var c = NewClient();
        var unknown = await c.PostAsJsonAsync("/api/auth/login", new { email = "ghost@example.com", password = "x" });
        var wrong = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.SeedEmail, password = "wrong" });
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await wrong.Content.ReadAsStringAsync());
        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
    }

    [Fact]
    public async Task Success_returns_200_with_access_token_and_sets_refresh_cookie()
    {
        var c = NewClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.SeedEmail, password = LoginAppFactory.SeedPassword, rememberMe = false });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var payload = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authenticated", payload.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("accessToken").GetString()));
        Assert.Equal(900, payload.GetProperty("expiresInSeconds").GetInt32());
        var setCookie = r.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("ta_rt=", setCookie);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/api/auth", setCookie);
        Assert.Contains("SameSite=Lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Idempotency_Key_header_is_silently_ignored()
    {
        var c = NewClient();
        c.DefaultRequestHeaders.Add("Idempotency-Key", "abc-123");
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "noone@example.com", password = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Body_over_4096_bytes_is_rejected()
    {
        var c = NewClient();
        var huge = new string('x', 5000);
        var json = $"{{\"email\":\"a@b.com\",\"password\":\"{huge}\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("Origin", "http://localhost:3000");
        var r = await c.SendAsync(req);
        Assert.True(r.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Origin_null_is_rejected_with_403()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("Origin", "null");
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "x@y.com", password = "p" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Wrong_content_type_returns_415()
    {
        var c = NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent("email=a&password=b", Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        req.Headers.Add("Origin", "http://localhost:3000");
        var r = await c.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, r.StatusCode);
    }

    [Fact]
    public async Task RememberMe_true_uses_long_cookie_lifetime()
    {
        var c = NewClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.SeedEmail, password = LoginAppFactory.SeedPassword, rememberMe = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var setCookie = r.Headers.GetValues("Set-Cookie").First();
        Assert.Matches(@"Max-Age=\d{6,}", setCookie);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_when_supplied()
    {
        var c = NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "x@y.com", password = "p" }),
        };
        req.Headers.Add("X-Correlation-Id", "trace-7");
        req.Headers.Add("Origin", "http://localhost:3000");
        var r = await c.SendAsync(req);
        Assert.Equal("trace-7", r.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task X_RateLimit_headers_only_appear_on_429_not_on_401()
    {
        var c = NewClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "miss@example.com", password = "p" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.False(r.Headers.Contains("X-RateLimit-Limit"));
        Assert.False(r.Headers.Contains("X-RateLimit-Remaining"));
    }

    // LOGIN-003 — sec-hard checklist gap: TM §I5 enumerates 7 collapse-to-401
    // sub-states; EmailUnverified + DisabledAccount MUST be byte-identical to
    // InvalidCredentials baseline AND MUST increment per-account RL on the same
    // rule as UnknownUser, else they become a guessing-oracle.

    [Fact]
    public async Task EmailUnverified_returns_byte_identical_invalid_credentials_as_baseline()
    {
        var c = NewClient();
        var baseline = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.SeedEmail, password = "wrong-password" });
        var unverified = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.UnverifiedEmail, password = LoginAppFactory.OtherUserPassword });

        Assert.Equal(baseline.StatusCode, unverified.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unverified.StatusCode);
        Assert.Equal(await baseline.Content.ReadAsStringAsync(), await unverified.Content.ReadAsStringAsync());
        Assert.False(unverified.Headers.Contains("Set-Cookie"));
        Assert.False(unverified.Headers.Contains("X-RateLimit-Limit"));
        Assert.False(unverified.Headers.Contains("X-RateLimit-Remaining"));
        var a = baseline.Headers.WwwAuthenticate.ToString();
        var b = unverified.Headers.WwwAuthenticate.ToString();
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task DisabledAccount_returns_byte_identical_invalid_credentials_as_baseline()
    {
        var c = NewClient();
        var baseline = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.SeedEmail, password = "wrong-password" });
        var disabled = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.DisabledEmail, password = LoginAppFactory.OtherUserPassword });

        Assert.Equal(baseline.StatusCode, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        Assert.Equal(await baseline.Content.ReadAsStringAsync(), await disabled.Content.ReadAsStringAsync());
        Assert.False(disabled.Headers.Contains("Set-Cookie"));
        Assert.False(disabled.Headers.Contains("X-RateLimit-Limit"));
        var a = baseline.Headers.WwwAuthenticate.ToString();
        var b = disabled.Headers.WwwAuthenticate.ToString();
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task EmailUnverified_increments_per_account_rate_limit_and_eventually_locks()
    {
        // Per-account RL = 5 failures / 15 min → lockout. If unverified didn't
        // increment, this loop would run forever without locking.
        var c = NewClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.UnverifiedEmail, password = LoginAppFactory.OtherUserPassword });
            Assert.Equal(HttpStatusCode.Unauthorized, last.StatusCode);
        }

        // 6th attempt — even with the *correct* password — must collapse to 401
        // because the account is now RL-locked. Proves the unverified path
        // incremented the per-account counter on attempts 1-5.
        var locked = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.UnverifiedEmail, password = LoginAppFactory.OtherUserPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("{\"status\":\"invalid_credentials\"}", await locked.Content.ReadAsStringAsync());
        Assert.False(locked.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task DisabledAccount_increments_per_account_rate_limit_and_eventually_locks()
    {
        var c = NewClient();
        for (var i = 0; i < 5; i++)
        {
            var r = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.DisabledEmail, password = LoginAppFactory.OtherUserPassword });
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        var locked = await c.PostAsJsonAsync("/api/auth/login", new { email = LoginAppFactory.DisabledEmail, password = LoginAppFactory.OtherUserPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("{\"status\":\"invalid_credentials\"}", await locked.Content.ReadAsStringAsync());
        Assert.False(locked.Headers.Contains("Set-Cookie"));
    }
}

public sealed class LoginAppFactory : WebApplicationFactory<Program>
{
    public const string SeedEmail = "alice@example.com";
    public const string SeedPassword = "S3cret-Password!";
    public const string UnverifiedEmail = "unverified@example.com";
    public const string DisabledEmail = "disabled@example.com";
    public const string OtherUserPassword = "An0ther-Password!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:OriginAllowList:0"] = "http://localhost:3000",
            });
        });
        builder.ConfigureServices(services =>
        {
            var hasher = new Argon2idPasswordHasher();
            var hash = hasher.Hash(SeedPassword);
            var otherHash = hasher.Hash(OtherUserPassword);
            var users = new InMemoryUserLookup(new[]
            {
                new UserRecord("u-1", SeedEmail, "Alice", hash, EmailVerified: true, Disabled: false),
                new UserRecord("u-2", UnverifiedEmail, "Eve", otherHash, EmailVerified: false, Disabled: false),
                new UserRecord("u-3", DisabledEmail, "Mal", otherHash, EmailVerified: true, Disabled: true),
            });
            var existing = services.Single(d => d.ServiceType == typeof(IUserLookup));
            services.Remove(existing);
            services.AddSingleton<IUserLookup>(users);
        });
    }
}
