using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 §8 #7 + RM-005 — single authoritative path for emitting the
// refresh-token cookie. Login handler MUST call AppendRefreshCookie; direct
// Response.Cookies.Append("ta_rt", ...) anywhere in src/**/Auth/ (except
// RefreshCookie*.cs) is blocked by semgrep `login-refresh-cookie-must-use-helper`
// AND by the contract-invariant gate.
public static class RefreshCookie
{
    public const string Name = "ta_rt";

    public static void AppendRefreshCookie(
        HttpContext ctx,
        IWebHostEnvironment env,
        string token,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(token);

        ctx.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            MaxAge = lifetime,
            IsEssential = true,
        });
    }

    public static void ClearRefreshCookie(HttpContext ctx, IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(env);
        ctx.Response.Cookies.Append(Name, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UnixEpoch,
        });
    }
}

public static class RefreshTokenLifetimes
{
    public static readonly TimeSpan Short = TimeSpan.FromHours(8);
    public static readonly TimeSpan Long = TimeSpan.FromDays(30);
    public static readonly TimeSpan LongAbsoluteCap = TimeSpan.FromDays(90);
}
