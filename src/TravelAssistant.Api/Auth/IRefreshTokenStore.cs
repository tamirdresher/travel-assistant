namespace TravelAssistant.Api.Auth;

/// <summary>
/// Swap seam: InMemoryRefreshTokenStore for tests / EF Core for prod.
/// </summary>
public interface IRefreshTokenStore
{
    Task SaveAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> FindAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RefreshToken> _store = new();

    public Task SaveAsync(RefreshToken token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        _store[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindAsync(string token, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(token, out var t) ? t : null);

    public Task RevokeAsync(string token, CancellationToken ct = default)
    {
        if (_store.TryGetValue(token, out var t))
            _store[token] = t with { Revoked = true };
        return Task.CompletedTask;
    }
}
