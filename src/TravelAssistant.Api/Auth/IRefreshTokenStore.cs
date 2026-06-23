using System.Collections.Concurrent;

namespace TravelAssistant.Api.Auth;

/// <summary>
/// Swap seam: in-memory for tests, EF Core / Cosmos for prod.
/// Family-aware: <see cref="RevokeFamilyAsync"/> implements D5-4 reuse-detection
/// and D5-5 password-change / logout-all revocation.
/// </summary>
public interface IRefreshTokenStore
{
    Task SaveAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> FindAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, string? rotatedTo, CancellationToken ct = default);
    Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default);
}

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshToken> _store = new();

    public Task SaveAsync(RefreshToken token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        _store[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindAsync(string token, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(token, out var t) ? t : null);

    public Task RevokeAsync(string token, string? rotatedTo, CancellationToken ct = default)
    {
        if (_store.TryGetValue(token, out var t))
            _store[token] = t with { Revoked = true, RotatedTo = rotatedTo };
        return Task.CompletedTask;
    }

    public Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default)
    {
        foreach (var kvp in _store)
        {
            if (kvp.Value.FamilyId == familyId && !kvp.Value.Revoked)
                _store[kvp.Key] = kvp.Value with { Revoked = true };
        }
        return Task.CompletedTask;
    }
}
