namespace TravelAssistant.Security.Pii;

/// <summary>
/// Per-tenant envelope-encryption gateway for PII fields. Implementations MUST:
/// <list type="bullet">
///   <item>Use AES-256-GCM as the data-encryption algorithm.</item>
///   <item>Wrap a per-tenant Data Encryption Key (DEK) with a Customer Managed Key (CMK) held in Key Vault.</item>
///   <item>Emit envelopes in the form <c>v1.{wrappedDek}.{nonce}.{ciphertextPlusTag}</c> (all base64url, no padding).</item>
///   <item>Be safe to call concurrently; DEKs may be cached in-process for up to 1 hour.</item>
///   <item>Round-trip null/empty inputs unchanged (no envelope on empty string, returns empty).</item>
/// </list>
/// Cryptographic erasure for GDPR Art.17 is performed by deleting the per-tenant DEK material
/// in Key Vault — the implementation does not own deletion, but envelopes encrypted with a
/// destroyed DEK MUST surface a non-retryable <see cref="PiiKeyUnavailableException"/> on decrypt.
/// </summary>
public interface IPiiCipher
{
    /// <summary>
    /// Encrypt a plaintext string for the given tenant. Returns an envelope string parseable by
    /// <see cref="DecryptAsync"/>. Returns <c>string.Empty</c> for null/empty input (round-trip property).
    /// </summary>
    /// <param name="tenantId">Tenant identifier; scopes the DEK. Must be non-empty.</param>
    /// <param name="plaintext">UTF-8 plaintext. May be null or empty.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> EncryptAsync(string tenantId, string? plaintext, CancellationToken ct = default);

    /// <summary>
    /// Decrypt an envelope produced by <see cref="EncryptAsync"/>. Returns <c>string.Empty</c>
    /// for null/empty input. Throws <see cref="PiiEnvelopeFormatException"/> for malformed envelopes
    /// and <see cref="PiiKeyUnavailableException"/> when the wrapping CMK or wrapped DEK is no
    /// longer accessible (e.g. cryptographic erasure).
    /// </summary>
    Task<string> DecryptAsync(string tenantId, string? envelope, CancellationToken ct = default);
}

/// <summary>Thrown when an envelope cannot be parsed (wrong version, wrong segment count, bad base64).</summary>
public sealed class PiiEnvelopeFormatException : Exception
{
    /// <summary>Create with a descriptive message.</summary>
    public PiiEnvelopeFormatException(string message) : base(message) { }
}

/// <summary>Thrown when the CMK or wrapped DEK is unavailable (e.g. tenant cryptographically erased).</summary>
public sealed class PiiKeyUnavailableException : Exception
{
    /// <summary>Create with a descriptive message.</summary>
    public PiiKeyUnavailableException(string message) : base(message) { }

    /// <summary>Create with a descriptive message and inner exception.</summary>
    public PiiKeyUnavailableException(string message, Exception inner) : base(message, inner) { }
}
