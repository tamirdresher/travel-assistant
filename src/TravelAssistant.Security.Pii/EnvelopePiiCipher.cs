using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TravelAssistant.Security.Pii;

/// <summary>
/// Options for <see cref="EnvelopePiiCipher"/>.
/// </summary>
public sealed class PiiCipherOptions
{
    /// <summary>How long an unwrapped DEK may live in memory. Default 1 hour.</summary>
    public TimeSpan DekCacheLifetime { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Default <see cref="IPiiCipher"/> implementation. Envelope:
/// <c>v1.{wrappedDek}.{nonce}.{ciphertextPlusTag}</c> with AES-256-GCM and per-tenant DEKs
/// wrapped by an <see cref="ICmkProvider"/>. Unwrapped DEKs are cached in-process for at most
/// <see cref="PiiCipherOptions.DekCacheLifetime"/>.
/// </summary>
public sealed class EnvelopePiiCipher : IPiiCipher
{
    private readonly ICmkProvider _cmk;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EnvelopePiiCipher> _log;
    private readonly TimeSpan _dekLifetime;

    /// <summary>DI constructor.</summary>
    public EnvelopePiiCipher(
        ICmkProvider cmk,
        IMemoryCache cache,
        IOptions<PiiCipherOptions> options,
        ILogger<EnvelopePiiCipher> log)
    {
        ArgumentNullException.ThrowIfNull(options);
        _cmk = cmk ?? throw new ArgumentNullException(nameof(cmk));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _dekLifetime = options.Value.DekCacheLifetime;
    }

    /// <inheritdoc/>
    public async Task<string> EncryptAsync(string tenantId, string? plaintext, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var (dek, wrapped) = await GetOrCreateDekAsync(tenantId, ct).ConfigureAwait(false);
        var nonce = RandomNumberGenerator.GetBytes(PiiEnvelope.NonceLength);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[pt.Length];
        var tag = new byte[PiiEnvelope.TagLength];

        using (var aes = new AesGcm(dek, PiiEnvelope.TagLength))
        {
            aes.Encrypt(nonce, pt, cipher, tag);
        }

        var ctAndTag = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, ctAndTag, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, ctAndTag, cipher.Length, tag.Length);
        return PiiEnvelope.Format(wrapped, nonce, ctAndTag);
    }

    /// <inheritdoc/>
    public async Task<string> DecryptAsync(string tenantId, string? envelope, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (string.IsNullOrEmpty(envelope))
        {
            return string.Empty;
        }

        var (wrappedDek, nonce, ctAndTag) = PiiEnvelope.Parse(envelope);

        byte[] dek;
        try
        {
            dek = await _cmk.UnwrapAsync(tenantId, wrappedDek, ct).ConfigureAwait(false);
        }
        catch (PiiKeyUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PiiKeyUnavailableException(
                $"CMK unwrap failed for tenant '{tenantId}' — possible cryptographic erasure", ex);
        }

        if (dek.Length != PiiEnvelope.DekLength)
        {
            throw new PiiEnvelopeFormatException(
                $"unwrapped DEK length {dek.Length} != {PiiEnvelope.DekLength}");
        }

        var cipherLen = ctAndTag.Length - PiiEnvelope.TagLength;
        var cipher = new byte[cipherLen];
        var tag = new byte[PiiEnvelope.TagLength];
        Buffer.BlockCopy(ctAndTag, 0, cipher, 0, cipherLen);
        Buffer.BlockCopy(ctAndTag, cipherLen, tag, 0, PiiEnvelope.TagLength);

        var pt = new byte[cipherLen];
        try
        {
            using var aes = new AesGcm(dek, PiiEnvelope.TagLength);
            aes.Decrypt(nonce, cipher, tag, pt);
        }
        catch (CryptographicException ex)
        {
            _log.LogWarning(ex, "PII envelope decrypt failed (tag mismatch) for tenant {Tenant}", tenantId);
            throw new PiiEnvelopeFormatException("authentication tag verification failed");
        }

        return Encoding.UTF8.GetString(pt);
    }

    private async Task<(byte[] Dek, byte[] Wrapped)> GetOrCreateDekAsync(string tenantId, CancellationToken ct)
    {
        if (_cache.TryGetValue<(byte[], byte[])>(CacheKey(tenantId), out var hit))
        {
            return hit;
        }

        var dek = RandomNumberGenerator.GetBytes(PiiEnvelope.DekLength);
        var wrapped = await _cmk.WrapAsync(tenantId, dek, ct).ConfigureAwait(false);
        var entry = (dek, wrapped);

        _cache.Set(CacheKey(tenantId), entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _dekLifetime,
            Size = 1,
        });
        return entry;
    }

    private static string CacheKey(string tenantId) => $"pii.dek::{tenantId}";
}
