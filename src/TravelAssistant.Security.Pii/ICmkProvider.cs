namespace TravelAssistant.Security.Pii;

/// <summary>
/// Wraps / unwraps a per-tenant Data Encryption Key with a Customer Managed Key (CMK).
/// Production implementation is backed by Azure Key Vault keys (AES key-wrap / RSA-OAEP);
/// tests substitute an in-memory provider.
/// </summary>
public interface ICmkProvider
{
    /// <summary>
    /// Wrap a 32-byte DEK with the CMK for the given tenant. Returned bytes are the wrapped DEK
    /// (opaque, includes any algorithm-specific framing). Throws <see cref="PiiKeyUnavailableException"/>
    /// if the CMK is missing or access is denied.
    /// </summary>
    Task<byte[]> WrapAsync(string tenantId, byte[] dek, CancellationToken ct);

    /// <summary>
    /// Unwrap a previously wrapped DEK. Throws <see cref="PiiKeyUnavailableException"/> if the
    /// CMK has been destroyed (cryptographic erasure) or access is denied.
    /// </summary>
    Task<byte[]> UnwrapAsync(string tenantId, byte[] wrappedDek, CancellationToken ct);
}
