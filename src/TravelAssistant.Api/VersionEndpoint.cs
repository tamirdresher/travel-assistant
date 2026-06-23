using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace TravelAssistant.Api;

// APP-8 — runtime version surface. Backs REL-4 release-notes acceptance.
// Source of truth for `version`: `version.txt` at content root (release-please
// writes this on every release). `commit` + `buildTime` are injected at
// build/container time via env vars (Dockerfile maps GITHUB_SHA / BUILD_TIME).
// Falls back to "unknown" / process start time when env is absent (local dev).
public sealed record VersionInfo(string Version, string Commit, string BuildTime);

public static class VersionEndpoint
{
    private const string VersionFileName = "version.txt";
    private const string CommitEnvVar = "GITHUB_SHA";
    private const string BuildTimeEnvVar = "BUILD_TIME";

    public static VersionInfo Load(IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);

        var version = ReadVersionFile(env.ContentRootPath);
        var commit = Environment.GetEnvironmentVariable(CommitEnvVar);
        if (string.IsNullOrWhiteSpace(commit))
        {
            commit = "unknown";
        }

        var buildTimeRaw = Environment.GetEnvironmentVariable(BuildTimeEnvVar);
        string buildTime;
        if (!string.IsNullOrWhiteSpace(buildTimeRaw)
            && DateTimeOffset.TryParse(buildTimeRaw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            buildTime = parsed.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            buildTime = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }

        return new VersionInfo(version, commit, buildTime);
    }

    public static IEndpointRouteBuilder MapVersionEndpoint(this IEndpointRouteBuilder endpoints, VersionInfo info)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(info);

        endpoints.MapGet("/api/version", () => Results.Ok(info))
            .WithName("VersionInfo")
            .WithTags("Meta")
            .Produces<VersionInfo>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static string ReadVersionFile(string contentRoot)
    {
        // Walk from content root upward looking for version.txt so the file works
        // in both container layout (copied to content root) and dev (repo root).
        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, VersionFileName);
            if (File.Exists(candidate))
            {
                var raw = File.ReadAllText(candidate).Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    return raw;
                }
            }

            dir = dir.Parent;
        }

        return "0.0.0";
    }
}
