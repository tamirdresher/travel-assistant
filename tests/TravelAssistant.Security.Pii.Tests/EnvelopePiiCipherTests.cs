using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace TravelAssistant.Security.Pii.Tests;

/// <summary>
/// The 6 mandatory unit tests from squads/security-hardening/artifacts/app-6-pii-encryption-spec.md v1.0.
/// Plus a handful of envelope-format guards.
/// </summary>
public sealed class EnvelopePiiCipherTests
{
    private static (EnvelopePiiCipher Cipher, InMemoryCmkProvider Cmk) Build(TimeSpan? dekLife = null)
    {
        var cmk = new InMemoryCmkProvider();
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 16 });
        var opts = Options.Create(new PiiCipherOptions
        {
            DekCacheLifetime = dekLife ?? TimeSpan.FromHours(1),
        });
        var cipher = new EnvelopePiiCipher(cmk, cache, opts, NullLogger<EnvelopePiiCipher>.Instance);
        return (cipher, cmk);
    }

    // ── Mandatory test 1 ──────────────────────────────────────────────────────
    [Fact]
    public async Task RoundTrip_PreservesPlaintext()
    {
        var (cipher, _) = Build();
        var pt = "Ellen Ripley, ellen.ripley@nostromo.example, +1-555-0100";

        var envelope = await cipher.EncryptAsync("tenant-a", pt);
        var back = await cipher.DecryptAsync("tenant-a", envelope);

        Assert.Equal(pt, back);
    }

    // ── Mandatory test 2 ──────────────────────────────────────────────────────
    [Fact]
    public async Task SamePlaintext_TwoEncrypts_ProduceDifferentEnvelopes()
    {
        var (cipher, _) = Build();
        var pt = "passport:ABC123";

        var e1 = await cipher.EncryptAsync("tenant-a", pt);
        var e2 = await cipher.EncryptAsync("tenant-a", pt);

        Assert.NotEqual(e1, e2);
        // But both decrypt to the same plaintext.
        Assert.Equal(pt, await cipher.DecryptAsync("tenant-a", e1));
        Assert.Equal(pt, await cipher.DecryptAsync("tenant-a", e2));
    }

    // ── Mandatory test 3 ──────────────────────────────────────────────────────
    [Fact]
    public async Task CrossTenant_Decrypt_Fails()
    {
        var (cipher, _) = Build();
        var envelope = await cipher.EncryptAsync("tenant-a", "ripley@nostromo.example");

        // Different tenant uses a different CMK, so unwrap fails (AES-GCM tag mismatch -> CryptographicException
        // inside the in-memory CMK provider, which our cipher maps to PiiKeyUnavailableException).
        await Assert.ThrowsAnyAsync<Exception>(() => cipher.DecryptAsync("tenant-b", envelope));
    }

    // ── Mandatory test 4 ──────────────────────────────────────────────────────
    [Fact]
    public async Task NullAndEmpty_RoundTrip_AsEmptyString()
    {
        var (cipher, _) = Build();

        Assert.Equal(string.Empty, await cipher.EncryptAsync("tenant-a", null));
        Assert.Equal(string.Empty, await cipher.EncryptAsync("tenant-a", string.Empty));
        Assert.Equal(string.Empty, await cipher.DecryptAsync("tenant-a", null));
        Assert.Equal(string.Empty, await cipher.DecryptAsync("tenant-a", string.Empty));
    }

    // ── Mandatory test 5 ──────────────────────────────────────────────────────
    [Fact]
    public async Task CryptographicErasure_ProducesPiiKeyUnavailable()
    {
        var (cipher, cmk) = Build();
        var envelope = await cipher.EncryptAsync("tenant-doomed", "secret");

        cmk.DestroyTenant("tenant-doomed");

        await Assert.ThrowsAsync<PiiKeyUnavailableException>(
            () => cipher.DecryptAsync("tenant-doomed", envelope));
    }

    // ── Mandatory test 6 ──────────────────────────────────────────────────────
    [Fact]
    public async Task DekCache_ReusesUnwrappedKey_WithinLifetime()
    {
        var (cipher, cmk) = Build(dekLife: TimeSpan.FromMinutes(5));

        var e1 = await cipher.EncryptAsync("tenant-a", "x");
        var wrapAfterFirst = cmk.WrapCallCount;
        var e2 = await cipher.EncryptAsync("tenant-a", "y");

        // Second encrypt MUST reuse the cached DEK — no new wrap call.
        Assert.Equal(wrapAfterFirst, cmk.WrapCallCount);
        Assert.NotEqual(e1, e2);
    }

    // ── Envelope-format guards (defense-in-depth, not in the mandatory list) ─
    [Theory]
    [InlineData("not-an-envelope")]
    [InlineData("v2.aaaa.bbbb.cccc")]
    [InlineData("v1.bad-base64!.bbbb.cccc")]
    [InlineData("v1.aGVsbG8.YWFh.YWFh")] // nonce wrong length
    public async Task MalformedEnvelope_Throws_PiiEnvelopeFormatException(string envelope)
    {
        var (cipher, _) = Build();
        await Assert.ThrowsAsync<PiiEnvelopeFormatException>(
            () => cipher.DecryptAsync("tenant-a", envelope));
    }

    [Fact]
    public async Task TamperedCiphertext_Throws_PiiEnvelopeFormatException()
    {
        var (cipher, _) = Build();
        var envelope = await cipher.EncryptAsync("tenant-a", "ripley");
        var parts = envelope.Split('.');
        // Flip a single bit in the ciphertext+tag segment by mutating its first char.
        var tampered = parts[3].StartsWith('A') ? "B" + parts[3][1..] : "A" + parts[3][1..];
        var bad = string.Join('.', parts[0], parts[1], parts[2], tampered);

        await Assert.ThrowsAsync<PiiEnvelopeFormatException>(
            () => cipher.DecryptAsync("tenant-a", bad));
    }

    [Fact]
    public async Task EmptyTenantId_Throws()
    {
        var (cipher, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(
            () => cipher.EncryptAsync(string.Empty, "x"));
    }
}
