using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TravelAssistant.Api.Auth;
using Xunit;

namespace TravelAssistant.Api.Tests;

/// <summary>
/// RM-004 acceptance: RememberMe controls refresh-token TTL and survives rotation.
/// </summary>
public sealed class RememberMeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RememberMeTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Login_WithoutRememberMe_IssuesShortTtl()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("alice@example.com", "pw"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(body!.RememberMe);
        Assert.Equal((int)RefreshTokenLifetimes.Short.TotalSeconds, body.RefreshTokenExpiresInSeconds);
    }

    [Fact]
    public async Task Login_WithRememberMeTrue_IssuesLongTtl()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("bob@example.com", "pw", RememberMe: true));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.True(body!.RememberMe);
        Assert.Equal((int)RefreshTokenLifetimes.Long.TotalSeconds, body.RefreshTokenExpiresInSeconds);
    }

    [Fact]
    public async Task Refresh_PreservesRememberMeFlagAndLongTtl()
    {
        var client = _factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("carol@example.com", "pw", RememberMe: true)))
            .Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);

        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var rotated = await refreshResp.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(rotated);
        Assert.True(rotated!.RememberMe);
        Assert.Equal((int)RefreshTokenLifetimes.Long.TotalSeconds, rotated.RefreshTokenExpiresInSeconds);
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);
    }

    [Fact]
    public async Task Refresh_PreservesShortTtlWhenRememberMeFalse()
    {
        var client = _factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("dan@example.com", "pw"))).Content.ReadFromJsonAsync<LoginResponse>();
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login!.RefreshToken));
        var rotated = await refreshResp.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.False(rotated!.RememberMe);
        Assert.Equal((int)RefreshTokenLifetimes.Short.TotalSeconds, rotated.RefreshTokenExpiresInSeconds);
    }

    [Fact]
    public async Task Refresh_RejectsRevokedToken()
    {
        var client = _factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("eve@example.com", "pw"))).Content.ReadFromJsonAsync<LoginResponse>();
        // First rotation succeeds and revokes the original
        await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login!.RefreshToken));
        // Reusing the now-revoked token must fail
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
}
