using System.Collections.Concurrent;

namespace TravelAssistant.Api.Checkout;

// In-memory idempotency cache. 24h TTL per design contract.
// Production: swap with a distributed cache (Redis) keyed by idempotency-key.
public interface IIdempotencyStore
{
    bool TryGet(string key, out CheckoutResponse response);
    void Save(string key, CheckoutResponse response);
}

internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, (CheckoutResponse Response, DateTimeOffset ExpiresAt)> _cache = new();

    public bool TryGet(string key, out CheckoutResponse response)
    {
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            response = entry.Response;
            return true;
        }
        response = null!;
        return false;
    }

    public void Save(string key, CheckoutResponse response)
    {
        _cache[key] = (response, DateTimeOffset.UtcNow.Add(Ttl));
    }
}
