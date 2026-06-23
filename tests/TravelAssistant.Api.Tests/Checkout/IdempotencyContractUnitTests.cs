using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace TravelAssistant.Api.Tests.Checkout;

// Unit-level tests for the *correct* IdempotencyStore contract per
// RFC draft-ietf-httpapi-idempotency-key-header §2.7.
//
// These tests run TODAY (no Skip) against a reference implementation that
// demonstrates the required behavior. When the production IdempotencyStore
// is fixed (issues #46 + #47), replace the reference with the production type
// and the suite will keep passing.
//
// Author: quality-testing-squad (Hockney)
public sealed class IdempotencyContractUnitTests
{
    [Fact]
    public void Save_Then_Get_With_Identical_Body_Returns_Hit_With_Original_Status()
    {
        var store = new ReferenceIdempotencyStore();
        var body = "{\"sessionId\":\"s1\",\"amount\":5000}";

        store.Save("K", statusCode: 402, body, hashOf: body);
        var outcome = store.Lookup("K", currentBody: body);

        Assert.Equal(IdempotencyLookup.Hit, outcome.Result);
        Assert.Equal(402, outcome.StatusCode);  // BUG-2 guard: status preserved
        Assert.Equal(body, outcome.Body);
    }

    [Fact]
    public void Save_Then_Get_With_Different_Body_Returns_Mismatch()
    {
        var store = new ReferenceIdempotencyStore();
        store.Save("K", statusCode: 200, "{\"sessionId\":\"victim\"}", hashOf: "{\"sessionId\":\"victim\"}");

        var outcome = store.Lookup("K", currentBody: "{\"sessionId\":\"attacker\"}");

        Assert.Equal(IdempotencyLookup.Mismatch, outcome.Result);  // BUG-1 guard
    }

    [Fact]
    public void Unknown_Key_Returns_Miss()
    {
        var store = new ReferenceIdempotencyStore();
        var outcome = store.Lookup("never-seen", currentBody: "{}");
        Assert.Equal(IdempotencyLookup.Miss, outcome.Result);
    }

    // ---- reference impl: the contract the production code MUST match ----

    private enum IdempotencyLookup { Miss, Hit, Mismatch }

    private readonly record struct LookupOutcome(IdempotencyLookup Result, int StatusCode, string? Body);

    private sealed class ReferenceIdempotencyStore
    {
        private readonly Dictionary<string, (int Status, string BodyHash, string Body)> _entries = new();

        public void Save(string key, int statusCode, string body, string hashOf)
            => _entries[key] = (statusCode, Sha256(hashOf), body);

        public LookupOutcome Lookup(string key, string currentBody)
        {
            if (!_entries.TryGetValue(key, out var entry))
                return new(IdempotencyLookup.Miss, 0, null);
            if (entry.BodyHash != Sha256(currentBody))
                return new(IdempotencyLookup.Mismatch, 0, null);
            return new(IdempotencyLookup.Hit, entry.Status, entry.Body);
        }

        private static string Sha256(string input)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
