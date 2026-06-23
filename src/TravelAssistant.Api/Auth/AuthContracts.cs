namespace TravelAssistant.Api.Auth;

/// <summary>
/// RM-004 (v2) login request. RememberMe opt-in (default false).
/// </summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

/// <summary>
/// RM-004 (v2). Refresh token is NOT in the body — it ships as an HttpOnly cookie (D5-3).
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    int AccessTokenExpiresInSeconds,
    int RefreshTokenExpiresInSeconds,
    bool RememberMe);

/// <summary>
/// RM-004 (v2) refresh response. Body carries only the new access token; the rotated refresh
/// token is set via Set-Cookie on the response. RememberMe is read from the DB row, never the request.
/// </summary>
public sealed record RefreshResponse(
    string AccessToken,
    int AccessTokenExpiresInSeconds,
    bool RememberMe);

/// <summary>
/// RM-004 (v2) lifetimes — binding per RM-005 §D5-2.
///   * access token        : 15 min (unchanged)
///   * refresh (unchecked) : 8h sliding
///   * refresh (remember)  : 30d sliding, 90d absolute cap from initial login
/// </summary>
public static class RefreshTokenLifetimes
{
    public static readonly TimeSpan AccessToken = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Short = TimeSpan.FromHours(8);
    public static readonly TimeSpan Long = TimeSpan.FromDays(30);
    public static readonly TimeSpan LongAbsoluteCap = TimeSpan.FromDays(90);

    public static TimeSpan For(bool rememberMe) => rememberMe ? Long : Short;
}

/// <summary>
/// RM-004 (v2) cookie contract — binding per RM-005 §D5-3 / §D5-7.
/// </summary>
public static class RefreshCookie
{
    public const string Name = "ta_rt";
    public const string Path = "/api/auth";
    public const string CsrfHeaderName = "X-TA-Refresh";
}
