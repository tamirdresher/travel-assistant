namespace TravelAssistant.Api.Auth;

/// <summary>
/// RM-004 login request. RememberMe is opt-in and defaults to false.
/// When true the server issues a long-lived refresh token (see <see cref="RefreshTokenLifetimes"/>).
/// </summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int AccessTokenExpiresInSeconds,
    int RefreshTokenExpiresInSeconds,
    bool RememberMe);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RefreshResponse(
    string AccessToken,
    string RefreshToken,
    int AccessTokenExpiresInSeconds,
    int RefreshTokenExpiresInSeconds,
    bool RememberMe);

/// <summary>
/// RM-004 lifetime contract. Confirmed with security-hardening-squad:
///   * short = 1 day  (default when RememberMe=false)
///   * long  = 30 days (when RememberMe=true)
///   * access token TTL is unchanged (15 min)
/// Stored on the RefreshToken row so /refresh honors the same window across rotation.
/// </summary>
public static class RefreshTokenLifetimes
{
    public static readonly TimeSpan AccessToken = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Short = TimeSpan.FromDays(1);
    public static readonly TimeSpan Long = TimeSpan.FromDays(30);

    public static TimeSpan For(bool rememberMe) => rememberMe ? Long : Short;
}
