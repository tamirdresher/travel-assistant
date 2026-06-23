using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TravelAssistant.Security.Pii.Tests;

/// <summary>
/// In-memory ICmkProvider for tests. AES key-wraps DEKs with a deterministic per-tenant CMK,
/// supports cryptographic erasure via <see cref="DestroyTenant"/>.
/// </summary>
internal sealed class InMemoryCmkProvider : ICmkProvider
{
    private readonly ConcurrentDictionary<string, byte[]> _cmks = new();
    private readonly HashSet<string> _destroyed = new();
    private readonly object _gate = new();

    public int WrapCallCount;
    public int UnwrapCallCount;

    public void DestroyTenant(string tenantId)
    {
        lock (_gate) { _destroyed.Add(tenantId); }
    }

    private byte[] GetCmk(string tenantId)
    {
        lock (_gate)
        {
            if (_destroyed.Contains(tenantId))
            {
                throw new PiiKeyUnavailableException($"tenant '{tenantId}' was cryptographically erased");
            }
        }
        return _cmks.GetOrAdd(tenantId, _ => RandomNumberGenerator.GetBytes(32));
    }

    public Task<byte[]> WrapAsync(string tenantId, byte[] dek, CancellationToken ct)
    {
        Interlocked.Increment(ref WrapCallCount);
        var cmk = GetCmk(tenantId);
        // Use AES-KW (RFC 3394) via Aes + ECB-like key-wrap. AesGcm is fine for an envelope-of-envelope here.
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ct2 = new byte[dek.Length];
        using (var aes = new AesGcm(cmk, 16))
        {
            aes.Encrypt(nonce, dek, ct2, tag);
        }
        var wrapped = new byte[nonce.Length + ct2.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, wrapped, 0, nonce.Length);
        Buffer.BlockCopy(ct2, 0, wrapped, nonce.Length, ct2.Length);
        Buffer.BlockCopy(tag, 0, wrapped, nonce.Length + ct2.Length, tag.Length);
        return Task.FromResult(wrapped);
    }

    public Task<byte[]> UnwrapAsync(string tenantId, byte[] wrappedDek, CancellationToken ct)
    {
        Interlocked.Increment(ref UnwrapCallCount);
        var cmk = GetCmk(tenantId);
        var nonce = wrappedDek[..12];
        var tag = wrappedDek[^16..];
        var cipher = wrappedDek[12..^16];
        var dek = new byte[cipher.Length];
        using var aes = new AesGcm(cmk, 16);
        aes.Decrypt(nonce, cipher, tag, dek);
        return Task.FromResult(dek);
    }
}
