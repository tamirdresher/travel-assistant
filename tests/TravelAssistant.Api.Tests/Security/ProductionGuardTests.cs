using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TravelAssistant.Api.Security;
using Xunit;

namespace TravelAssistant.Api.Tests.Security;

/// <summary>
/// SEC-5 — Verifies ProductionGuard fails on dev-only switches in
/// non-Development environments and skips checks in Development.
/// </summary>
public class ProductionGuardTests
{
    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "TravelAssistant.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static IConfiguration Config(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Development_SkipsAllChecks()
    {
        var env = new FakeEnv { EnvironmentName = "Development" };
        var cfg = Config(new()
        {
            ["ConnectionStrings:cosmos"] = "AccountEndpoint=https://localhost:8081;AccountKey=abc",
            ["Cors:AllowedOrigins:0"] = "*",
        });

        var report = new ProductionGuard(cfg, env).Run();
        Assert.True(report.Ok);
    }

    [Fact]
    public void Production_FailsOnCosmosEmulator()
    {
        var env = new FakeEnv();
        var cfg = Config(new()
        {
            ["ConnectionStrings:cosmos"] = "AccountEndpoint=https://localhost:8081;AccountKey=abc",
            ["Cors:AllowedOrigins:0"] = "https://app.example.com",
            ["KeyVault:Uri"] = "https://example-kv.vault.azure.net/",
        });

        var report = new ProductionGuard(cfg, env).Run();
        Assert.False(report.Ok);
        Assert.Contains(report.Checks, c => c.Name == "emulators" && !c.Passed);
    }

    [Fact]
    public void Production_FailsOnWildcardCors()
    {
        var env = new FakeEnv();
        var cfg = Config(new()
        {
            ["Cors:AllowedOrigins:0"] = "*",
            ["KeyVault:Uri"] = "https://example-kv.vault.azure.net/",
        });

        var report = new ProductionGuard(cfg, env).Run();
        Assert.False(report.Ok);
        Assert.Contains(report.Checks, c => c.Name == "cors" && !c.Passed);
    }

    [Fact]
    public void Production_FailsOnMissingKeyVault()
    {
        var env = new FakeEnv();
        var cfg = Config(new()
        {
            ["Cors:AllowedOrigins:0"] = "https://app.example.com",
        });

        var report = new ProductionGuard(cfg, env).Run();
        Assert.False(report.Ok);
        Assert.Contains(report.Checks, c => c.Name == "keyvault" && !c.Passed);
    }

    [Fact]
    public void Production_FailsWhenLocalhostSsrfAllowed()
    {
        var env = new FakeEnv();
        var cfg = Config(new()
        {
            ["Cors:AllowedOrigins:0"] = "https://app.example.com",
            ["KeyVault:Uri"] = "https://example-kv.vault.azure.net/",
            ["Security:Ssrf:IsLocalhostAllowed"] = "true",
        });

        var report = new ProductionGuard(cfg, env).Run();
        Assert.False(report.Ok);
        Assert.Contains(report.Checks, c => c.Name == "ssrf-localhost" && !c.Passed);
    }

    [Fact]
    public void Production_PassesWhenAllSwitchesSafe()
    {
        var env = new FakeEnv();
        var cfg = Config(new()
        {
            ["Cors:AllowedOrigins:0"] = "https://app.example.com",
            ["KeyVault:Uri"] = "https://example-kv.vault.azure.net/",
            ["Security:Ssrf:IsLocalhostAllowed"] = "false",
        });

        var report = new ProductionGuard(cfg, env).Run();
        Assert.True(report.Ok);
    }
}
