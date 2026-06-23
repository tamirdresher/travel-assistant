namespace TravelAssistant.Api.Auth;

public sealed record UserRecord(
    string Id,
    string Email,
    string DisplayName,
    string PasswordHash,
    bool EmailVerified,
    bool Disabled);

// Stand-in user lookup until the persistence layer lands. Swap this for an
// EF/Dapper-backed implementation; the endpoint depends only on the interface.
public interface IUserLookup
{
    UserRecord? FindByEmail(string email);
}

public sealed class InMemoryUserLookup : IUserLookup
{
    private readonly Dictionary<string, UserRecord> _byEmail;

    public InMemoryUserLookup(IEnumerable<UserRecord> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _byEmail = new Dictionary<string, UserRecord>(StringComparer.Ordinal);
        foreach (var u in seed)
        {
            _byEmail[NormalizeEmail(u.Email)] = u;
        }
    }

    public UserRecord? FindByEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return _byEmail.TryGetValue(NormalizeEmail(email), out var u) ? u : null;
    }

    internal static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
