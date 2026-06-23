using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TravelAssistant.Security.Pii;

/// <summary>DI helpers for APP-6 PII encryption.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IPiiCipher"/> (envelope, AES-256-GCM) backed by Azure Key Vault for CMK
    /// wrap/unwrap. Reads <c>Pii:KeyVault:VaultUri</c>, <c>Pii:KeyVault:CmkName</c>, optional
    /// <c>Pii:KeyVault:WrapAlgorithm</c>, and optional <c>Pii:DekCacheLifetime</c> (TimeSpan) from
    /// configuration. Uses <see cref="DefaultAzureCredential"/> for AAD auth.
    /// </summary>
    public static IServiceCollection AddPiiEncryption(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<KeyVaultCmkOptions>(config.GetSection("Pii:KeyVault"));
        services.Configure<PiiCipherOptions>(config.GetSection("Pii"));

        services.AddMemoryCache(o => o.SizeLimit = 1024);
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton<ICmkProvider, KeyVaultCmkProvider>();
        services.AddSingleton<IPiiCipher, EnvelopePiiCipher>();
        return services;
    }

    /// <summary>
    /// Register <see cref="IPiiCipher"/> with a caller-supplied <see cref="ICmkProvider"/>.
    /// Use this from tests or when CMK material lives outside Key Vault.
    /// </summary>
    public static IServiceCollection AddPiiEncryption(
        this IServiceCollection services,
        ICmkProvider cmkProvider,
        TimeSpan? dekCacheLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cmkProvider);

        services.AddMemoryCache(o => o.SizeLimit = 1024);
        services.Configure<PiiCipherOptions>(o =>
        {
            if (dekCacheLifetime is { } life)
            {
                o.DekCacheLifetime = life;
            }
        });
        services.AddSingleton(cmkProvider);
        services.AddSingleton<IPiiCipher, EnvelopePiiCipher>();
        return services;
    }
}
