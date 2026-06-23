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
}

public sealed class LoginAppFactory : WebApplicationFactory<Program>
{
    public const string SeedEmail = "alice@example.com";
    public const string SeedPassword = "S3cret-Password!";

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
            var users = new InMemoryUserLookup(new[]
            {
                new UserRecord("u-1", SeedEmail, "Alice", hash, EmailVerified: true, Disabled: false),
            });
            var existing = services.Single(d => d.ServiceType == typeof(IUserLookup));
            services.Remove(existing);
            services.AddSingleton<IUserLookup>(users);
        });
    }
}
