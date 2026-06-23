using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TravelAssistant.Api;
using Xunit;

namespace TravelAssistant.Api.Tests;

// APP-8 acceptance: GET /api/version returns 200 with the three required fields,
// and `version` matches the value in version.txt at content root.
public sealed class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VersionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetVersion_returns_200_with_required_fields()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/version", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<VersionInfo>();
        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info!.Version), "version must be non-empty");
        Assert.False(string.IsNullOrWhiteSpace(info.Commit), "commit must be non-empty");
        Assert.False(string.IsNullOrWhiteSpace(info.BuildTime), "buildTime must be non-empty");
    }

    [Fact]
    public async Task GetVersion_version_matches_version_txt()
    {
        using var client = _factory.CreateClient();
        var info = await client.GetFromJsonAsync<VersionInfo>(new Uri("/api/version", UriKind.Relative));
        Assert.NotNull(info);

        var expected = await File.ReadAllTextAsync(LocateVersionFile());
        Assert.Equal(expected.Trim(), info!.Version);
    }

    private static string LocateVersionFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "version.txt");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("version.txt not found from test base directory");
    }
}
