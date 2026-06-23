using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TravelAssistant.Security.Pii;

/// <summary>Options for <see cref="KeyVaultCmkProvider"/>.</summary>
public sealed class KeyVaultCmkOptions
{
    /// <summary>The Key Vault URI (e.g. <c>https://my-vault.vault.azure.net/</c>). Required.</summary>
    public string VaultUri { get; set; } = string.Empty;

    /// <summary>The name of the CMK in the vault. Required.</summary>
    public string CmkName { get; set; } = string.Empty;

    /// <summary>Wrap algorithm. Default <c>RSA-OAEP-256</c> (works for RSA CMKs).</summary>
    public string WrapAlgorithm { get; set; } = "RSA-OAEP-256";
}

/// <summary>
/// Azure Key Vault CMK provider. Uses the per-tenant id only for logging — every wrap/unwrap
/// goes through the same CMK named in <see cref="KeyVaultCmkOptions.CmkName"/>. Tenant isolation
/// comes from the DEK being randomly generated per tenant and cached separately.
/// Maps Key Vault not-found / forbidden errors to <see cref="PiiKeyUnavailableException"/> so
/// callers can distinguish cryptographic erasure from programming errors.
/// </summary>
public sealed class KeyVaultCmkProvider : ICmkProvider
{
    private readonly CryptographyClient _client;
    private readonly KeyWrapAlgorithm _alg;
    private readonly ILogger<KeyVaultCmkProvider> _log;

    /// <summary>DI constructor.</summary>
    public KeyVaultCmkProvider(
        IOptions<KeyVaultCmkOptions> options,
        TokenCredential credential,
        ILogger<KeyVaultCmkProvider> log)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.VaultUri))
        {
            throw new InvalidOperationException("KeyVaultCmkOptions.VaultUri is required");
        }
        if (string.IsNullOrWhiteSpace(opts.CmkName))
        {
            throw new InvalidOperationException("KeyVaultCmkOptions.CmkName is required");
        }

        var keyId = new Uri(new Uri(opts.VaultUri), $"keys/{opts.CmkName}");
        _client = new CryptographyClient(keyId, credential);
        _alg = new KeyWrapAlgorithm(opts.WrapAlgorithm);
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc/>
    public async Task<byte[]> WrapAsync(string tenantId, byte[] dek, CancellationToken ct)
    {
        try
        {
            var result = await _client.WrapKeyAsync(_alg, dek, ct).ConfigureAwait(false);
            return result.EncryptedKey;
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403 or 404)
        {
            _log.LogError(ex, "CMK wrap failed for tenant {Tenant} (status {Status})", tenantId, ex.Status);
            throw new PiiKeyUnavailableException(
                $"CMK wrap denied or key missing (status {ex.Status})", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> UnwrapAsync(string tenantId, byte[] wrappedDek, CancellationToken ct)
    {
        try
        {
            var result = await _client.UnwrapKeyAsync(_alg, wrappedDek, ct).ConfigureAwait(false);
            return result.Key;
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403 or 404)
        {
            _log.LogError(ex, "CMK unwrap failed for tenant {Tenant} (status {Status}) — possible erasure",
                tenantId, ex.Status);
            throw new PiiKeyUnavailableException(
                $"CMK unwrap denied or key missing (status {ex.Status}) — possible cryptographic erasure", ex);
        }
    }
}
