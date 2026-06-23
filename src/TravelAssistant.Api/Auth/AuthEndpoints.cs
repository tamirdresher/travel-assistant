using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace TravelAssistant.Api.Auth;

/// <summary>
/// RM-004 (v2) auth endpoints. Binding contract: docs/security/remember-me-threat-model.md §D5/§6.
///
/// /login    issues access token in body + refresh token in HttpOnly Secure SameSite=Lax cookie (D5-3).
/// /refresh  reads cookie, requires X-TA-Refresh CSRF header (§5), rotates token preserving FamilyId
///           (D5-4). On reuse of a revoked token: revoke the entire family + force re-login.
/// /logout   revokes the current family (D5-5).
///
/// `RememberMe` on /refresh is read from the DB row, NEVER from the request — Semgrep rule
/// `no-rememberme-trust-from-request-on-refresh` enforces this.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest req,
            HttpContext ctx,
            IRefreshTokenStore store,
            TimeProvider clock,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "INVALID_CREDENTIALS" });

            // Real credential verification is owned by security-hardening-squad's IAuthService
            // (out of scope for RM-003/004). This scaffold mints tokens for any non-empty pair so
            // the remember-me wiring is exercisable end-to-end.
            var userId = req.Email.ToLowerInvariant();
            var now = clock.GetUtcNow();
            var lifetime = RefreshTokenLifetimes.For(req.RememberMe);
            var familyId = Guid.NewGuid();

            var refresh = new RefreshToken(
                Token: GenerateOpaqueToken(),
                UserId: userId,
                FamilyId: familyId,
                FamilyOriginAt: now,
                IssuedAt: now,
                ExpiresAt: now + lifetime,
                RememberMe: req.RememberMe);
            await store.SaveAsync(refresh, ct);

            AppendRefreshCookie(ctx, env, refresh.Token, lifetime);

            return Results.Ok(new LoginResponse(
                AccessToken: GenerateOpaqueToken(),
                AccessTokenExpiresInSeconds: (int)RefreshTokenLifetimes.AccessToken.TotalSeconds,
                RefreshTokenExpiresInSeconds: (int)lifetime.TotalSeconds,
                RememberMe: req.RememberMe));
        })
        .WithName("Login")
        .WithTags("Auth");

        app.MapPost("/api/auth/refresh", async (
            HttpContext ctx,
            IRefreshTokenStore store,
            TimeProvider clock,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            // §5 CSRF gate — browsers cannot send custom headers cross-origin without preflight.
            if (!ctx.Request.Headers.TryGetValue(RefreshCookie.CsrfHeaderName, out var csrf)
                || string.IsNullOrEmpty(csrf))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!ctx.Request.Cookies.TryGetValue(RefreshCookie.Name, out var presented)
                || string.IsNullOrWhiteSpace(presented))
                return Results.Unauthorized();

            var existing = await store.FindAsync(presented, ct);
            var now = clock.GetUtcNow();

            // D5-4 reuse detection: a revoked token presented again ⇒ revoke the entire family.
            if (existing is { Revoked: true })
            {
                await store.RevokeFamilyAsync(existing.FamilyId, ct);
                ClearRefreshCookie(ctx, env);
                return Results.Unauthorized();
            }

            if (existing is null || existing.ExpiresAt <= now)
            {
                ClearRefreshCookie(ctx, env);
                return Results.Unauthorized();
            }

            // D5-2 absolute cap on remember-me families (90d from login origin).
            if (existing.RememberMe && now - existing.FamilyOriginAt > RefreshTokenLifetimes.LongAbsoluteCap)
            {
                await store.RevokeFamilyAsync(existing.FamilyId, ct);
                ClearRefreshCookie(ctx, env);
                return Results.Unauthorized();
            }

            // RememberMe comes from the DB row (existing), NEVER the request body.
            var lifetime = RefreshTokenLifetimes.For(existing.RememberMe);
            var rotated = new RefreshToken(
                Token: GenerateOpaqueToken(),
                UserId: existing.UserId,
                FamilyId: existing.FamilyId,
                FamilyOriginAt: existing.FamilyOriginAt,
                IssuedAt: now,
                ExpiresAt: now + lifetime,
                RememberMe: existing.RememberMe);

            await store.SaveAsync(rotated, ct);
            await store.RevokeAsync(existing.Token, rotated.Token, ct);

            AppendRefreshCookie(ctx, env, rotated.Token, lifetime);

            return Results.Ok(new RefreshResponse(
                AccessToken: GenerateOpaqueToken(),
                AccessTokenExpiresInSeconds: (int)RefreshTokenLifetimes.AccessToken.TotalSeconds,
                RememberMe: rotated.RememberMe));
        })
        .WithName("RefreshAuth")
        .WithTags("Auth");

        app.MapPost("/api/auth/logout", async (
            HttpContext ctx,
            IRefreshTokenStore store,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            if (!ctx.Request.Headers.TryGetValue(RefreshCookie.CsrfHeaderName, out var csrf)
                || string.IsNullOrEmpty(csrf))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (ctx.Request.Cookies.TryGetValue(RefreshCookie.Name, out var presented)
                && !string.IsNullOrWhiteSpace(presented))
            {
                var row = await store.FindAsync(presented, ct);
                if (row is not null)
                    await store.RevokeFamilyAsync(row.FamilyId, ct);
            }

            ClearRefreshCookie(ctx, env);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithTags("Auth");

        return app;
    }

    private static void AppendRefreshCookie(HttpContext ctx, IWebHostEnvironment env, string token, TimeSpan lifetime)
    {
        ctx.Response.Cookies.Append(RefreshCookie.Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookie.Path,
            MaxAge = lifetime,
            IsEssential = true,
        });
    }

    private static void ClearRefreshCookie(HttpContext ctx, IWebHostEnvironment env)
    {
        ctx.Response.Cookies.Append(RefreshCookie.Name, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookie.Path,
            MaxAge = TimeSpan.Zero,
            Expires = DateTimeOffset.UnixEpoch,
            IsEssential = true,
        });
    }

    private static string GenerateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
