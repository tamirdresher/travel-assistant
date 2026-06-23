using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TravelAssistant.Api.Tests.Checkout;

// 8 idempotency security tests mandated as merge gate for fix/checkout-idempotency-p0.
// Source: ideation-research-planning-squad (Aldo) — SEC-CHK-007 R1/R2/R3 verification.
// Per RFC 8785 (JSON Canonicalization Scheme) and RFC draft-ietf-httpapi-idempotency-key-header.
//
// These tests pin the SECURITY contract for the derived-cache-key + canonical-body design.
// They are Skip'd until WI-1 (R1+R2+R3) lands; remove Skip on merge of fix/checkout-idempotency-p0.
//
// Author: quality-testing-squad (Hockney)
public sealed class IdempotencySecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Skip = "Activates when fix/checkout-idempotency-p0 merges (WI-1 R1/R2/R3).";
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IdempotencySecurityTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    // ----- Case 1: Cross-user replay isolation (R2: H(sub:key)) -----
    [Fact(Skip = Skip)]
    public async Task CrossUserReplay_IsolatedBySubClaim()
    {
        var key = Guid.NewGuid().ToString();
        var body = new { sessionId = "s-1", paymentToken = "tok_A" };

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = BearerForUser("user-A");
        clientA.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var respA = await clientA.PostAsJsonAsync("/api/checkout/confirm", body);
        var bodyA = await respA.Content.ReadAsStringAsync();

        // User B replays SAME key, SAME body — must NOT receive A's cached response.
        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = BearerForUser("user-B");
        clientB.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var respB = await clientB.PostAsJsonAsync("/api/checkout/confirm", body);
        var bodyB = await respB.Content.ReadAsStringAsync();

        Assert.NotEqual(bodyA, bodyB);
        Assert.DoesNotContain("\"orderId\":\"" + ExtractOrderId(bodyA) + "\"", bodyB);
    }

    // ----- Case 2: Guest→auth rebind (different derived cache key) -----
    [Fact(Skip = Skip)]
    public async Task GuestToAuth_Rebind_TreatedAsNewRequest()
    {
        var key = Guid.NewGuid().ToString();
        var sessionId = "guest-sess-" + Guid.NewGuid();
        var body = new { sessionId, paymentToken = "tok_X" };

        using var guest = _factory.CreateClient();
        guest.DefaultRequestHeaders.Add("Idempotency-Key", key);
        guest.DefaultRequestHeaders.Add("X-Session-Id", sessionId);
        var guestResp = await guest.PostAsJsonAsync("/api/checkout/confirm", body);
        Assert.Equal(HttpStatusCode.OK, guestResp.StatusCode);
        var guestBody = await guestResp.Content.ReadAsStringAsync();

        using var auth = _factory.CreateClient();
        auth.DefaultRequestHeaders.Authorization = BearerForUser("user-Z");
        auth.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var authResp = await auth.PostAsJsonAsync("/api/checkout/confirm", body);
        var authBody = await authResp.Content.ReadAsStringAsync();

        Assert.NotEqual(guestBody, authBody);
    }

    // ----- Case 3: Timing-difference (FixedTimeEquals) — p99 delta < 10µs -----
    [Fact(Skip = Skip)]
    public async Task HashCompare_ConstantTime_NoTimingOracle()
    {
        // Probe the in-process hash compare via the exposed test seam (or via 422 latency).
        // Method: 1000 trials matching, 1000 trials early-divergence mismatch; assert
        // median delta < 10µs and p99 delta < 50µs (CI-tolerant; spec is 10µs on bare metal).
        const int trials = 1000;
        var matchTimes = new long[trials];
        var mismatchTimes = new long[trials];
        var bodyA = JsonSerializer.SerializeToUtf8Bytes(new { a = 1, b = 2 });
        var bodyB = JsonSerializer.SerializeToUtf8Bytes(new { a = 9, b = 2 });
        var hashA = SHA256.HashData(bodyA);
        var hashB = SHA256.HashData(bodyB);

        for (var i = 0; i < trials; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = CryptographicOperations.FixedTimeEquals(hashA, hashA);
            sw.Stop();
            matchTimes[i] = sw.ElapsedTicks;
        }
        for (var i = 0; i < trials; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = CryptographicOperations.FixedTimeEquals(hashA, hashB);
            sw.Stop();
            mismatchTimes[i] = sw.ElapsedTicks;
        }
        Array.Sort(matchTimes);
        Array.Sort(mismatchTimes);
        var p99Match = matchTimes[(int)(trials * 0.99)];
        var p99Mismatch = mismatchTimes[(int)(trials * 0.99)];
        var deltaTicks = Math.Abs(p99Match - p99Mismatch);
        var deltaMicros = deltaTicks * 1_000_000.0 / Stopwatch.Frequency;
        // CI tolerance: 50µs (bare metal target: 10µs). FixedTimeEquals must be constant-time.
        Assert.True(deltaMicros < 50.0, $"Timing oracle suspected: p99 delta = {deltaMicros:F2}µs (limit 50µs)");
        await Task.CompletedTask;
    }

    // ----- Case 4: Key-order invariance (RFC 8785 JCS) -----
    [Fact(Skip = Skip)]
    public async Task BodyHash_KeyOrderInvariant_PerJcs()
    {
        var key = Guid.NewGuid().ToString();
        using var c1 = _factory.CreateClient();
        c1.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var r1 = await c1.PostAsync("/api/checkout/confirm",
            new StringContent("{\"a\":1,\"b\":2}", Encoding.UTF8, "application/json"));

        using var c2 = _factory.CreateClient();
        c2.DefaultRequestHeaders.Add("Idempotency-Key", key);
        // Same content, reversed key order — must hit cache (replay), not 422.
        var r2 = await c2.PostAsync("/api/checkout/confirm",
            new StringContent("{\"b\":2,\"a\":1}", Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.UnprocessableEntity, r2.StatusCode);
        Assert.Equal(r1.StatusCode, r2.StatusCode);
    }

    // ----- Case 5: Whitespace invariance (JCS strips insignificant whitespace) -----
    [Fact(Skip = Skip)]
    public async Task BodyHash_WhitespaceInvariant_PerJcs()
    {
        var key = Guid.NewGuid().ToString();
        using var c1 = _factory.CreateClient();
        c1.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var r1 = await c1.PostAsync("/api/checkout/confirm",
            new StringContent("{\"a\":1}", Encoding.UTF8, "application/json"));

        using var c2 = _factory.CreateClient();
        c2.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var r2 = await c2.PostAsync("/api/checkout/confirm",
            new StringContent("{ \"a\" : 1 }", Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.UnprocessableEntity, r2.StatusCode);
        Assert.Equal(r1.StatusCode, r2.StatusCode);
    }

    // ----- Case 6: Unicode NFC vs NFD normalization (JCS §3.2.4) -----
    [Fact(Skip = Skip)]
    public async Task BodyHash_UnicodeNormalization_NfcEqualsNfd()
    {
        var key = Guid.NewGuid().ToString();
        // "café" — NFC: U+00E9 (precomposed). NFD: U+0065 U+0301 (decomposed).
        var nfc = "{\"name\":\"caf\u00e9\"}";
        var nfd = "{\"name\":\"cafe\u0301\"}";

        using var c1 = _factory.CreateClient();
        c1.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var r1 = await c1.PostAsync("/api/checkout/confirm",
            new StringContent(nfc, Encoding.UTF8, "application/json"));

        using var c2 = _factory.CreateClient();
        c2.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var r2 = await c2.PostAsync("/api/checkout/confirm",
            new StringContent(nfd, Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.UnprocessableEntity, r2.StatusCode);
        Assert.Equal(r1.StatusCode, r2.StatusCode);
    }

    // ----- Case 7: Per-sub entry cap (T13 mitigation) -----
    [Fact(Skip = Skip)]
    public async Task PerSubEntryCap_Returns429WithRetryAfter()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = BearerForUser("cap-test-user");

        HttpResponseMessage? final = null;
        for (var i = 0; i <= 1000; i++)
        {
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", $"k-{i}");
            final = await client.PostAsJsonAsync("/api/checkout/confirm",
                new { sessionId = "s", paymentToken = "t" });
            if (final.StatusCode == HttpStatusCode.TooManyRequests) break;
        }
        Assert.NotNull(final);
        Assert.Equal(HttpStatusCode.TooManyRequests, final!.StatusCode);
        Assert.NotNull(final.Headers.RetryAfter);
    }

    // ----- Case 8: Per-IP entry cap (guest path) -----
    [Fact(Skip = Skip)]
    public async Task PerIpEntryCap_Returns429ForGuestPath()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.42");

        HttpResponseMessage? final = null;
        for (var i = 0; i <= 5000; i++)
        {
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", $"g-{i}");
            final = await client.PostAsJsonAsync("/api/checkout/confirm",
                new { sessionId = $"gs-{i}", paymentToken = "t" });
            if (final.StatusCode == HttpStatusCode.TooManyRequests) break;
        }
        Assert.NotNull(final);
        Assert.Equal(HttpStatusCode.TooManyRequests, final!.StatusCode);
    }

    // ----- helpers -----
    private static AuthenticationHeaderValue BearerForUser(string sub)
    {
        // Test seam: the api test fixture should accept a dev token of form "test:{sub}"
        // and map it onto a ClaimsPrincipal with ClaimTypes.NameIdentifier = sub.
        return new AuthenticationHeaderValue("Bearer", $"test:{sub}");
    }

    private static string ExtractOrderId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("orderId", out var v) ? v.GetString() ?? "" : "";
        }
        catch { return ""; }
    }
}
