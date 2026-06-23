using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TravelAssistant.Api.Security;

/// <summary>
/// SEC-5 — Refuses to start the app in any non-Development environment
/// if a developer convenience is still wired up. Run twice: once at
/// startup (hard gate) and once via /health/prod-guard (diagnostic for
/// INF-4 deploy gate).
/// </summary>
public sealed class ProductionGuard
{
    public sealed record CheckResult(string Name, bool Passed, string Message);

    public sealed record GuardReport(bool Ok, IReadOnlyList<CheckResult> Checks);

    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public ProductionGuard(IConfiguration config, IHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public static bool IsRequired(IHostEnvironment env)
        => !env.IsDevelopment();

    public GuardReport Run()
    {
        var checks = new List<CheckResult>
        {
            CheckEmulators(),
            CheckCors(),
            CheckKeyVault(),
            CheckSsrfLocalhost(),
        };

        return new GuardReport(checks.All(c => c.Passed), checks);
    }

    private CheckResult CheckEmulators()
    {
        if (_env.IsDevelopment())
        {
            return new("emulators", true, "skipped in Development");
        }

        string[] suspect =
        [
            "ConnectionStrings:cosmos",
            "ConnectionStrings:storage",
            "Cosmos:Endpoint",
            "Storage:Endpoint",
        ];

        foreach (var key in suspect)
        {
            var v = _config[key];
            if (string.IsNullOrEmpty(v))
            {
                continue;
            }

            if (LooksLikeEmulator(v))
            {
                return new("emulators", false,
                    $"emulator endpoint detected in '{key}' (must not run outside Development)");
            }
        }

        return new("emulators", true, "no emulator endpoints");
    }

    private static bool LooksLikeEmulator(string value)
    {
        var v = value.ToLowerInvariant();
        return v.Contains("localhost")
            || v.Contains("127.0.0.1")
            || v.Contains("host.docker.internal")
            || v.Contains("usedevelopmentstorage=true")
            || v.Contains("devstoreaccount1");
    }

    private CheckResult CheckCors()
    {
        if (_env.IsDevelopment())
        {
            return new("cors", true, "skipped in Development");
        }

        var origins = _config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(o => o == "*"))
        {
            return new("cors", false, "Cors:AllowedOrigins contains '*' outside Development");
        }

        return new("cors", true, $"explicit origins configured ({origins.Length})");
    }

    private CheckResult CheckKeyVault()
    {
        if (_env.IsDevelopment())
        {
            return new("keyvault", true, "skipped in Development");
        }

        var uri = _config["KeyVault:Uri"];
        if (string.IsNullOrWhiteSpace(uri))
        {
            return new("keyvault", false, "KeyVault:Uri must be set outside Development");
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !parsed.Host.EndsWith(".vault.azure.net", StringComparison.OrdinalIgnoreCase))
        {
            return new("keyvault", false, $"KeyVault:Uri '{uri}' is not a valid Azure Key Vault URI");
        }

        return new("keyvault", true, "Key Vault URI present and well-formed");
    }

    private CheckResult CheckSsrfLocalhost()
    {
        if (_env.IsDevelopment())
        {
            return new("ssrf-localhost", true, "skipped in Development");
        }

        var allowed = _config.GetValue<bool?>("Security:Ssrf:IsLocalhostAllowed") ?? false;
        return allowed
            ? new("ssrf-localhost", false,
                "Security:Ssrf:IsLocalhostAllowed must be false outside Development (SEC-3)")
            : new("ssrf-localhost", true, "localhost outbound disallowed");
    }
}

public sealed class ProductionGuardException : Exception
{
    public ProductionGuardException(IEnumerable<ProductionGuard.CheckResult> failed)
        : base("ProductionGuard refused to start the app: "
               + string.Join("; ", failed.Select(c => $"{c.Name}: {c.Message}")))
    {
    }
}
