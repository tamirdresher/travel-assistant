using System.Security.Cryptography;

namespace TravelAssistant.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // RM-004: login. RememberMe defaults to false; when true, longer refresh-token TTL.
        app.MapPost("/api/auth/login", async (LoginRequest req, IRefreshTokenStore store, TimeProvider clock, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "INVALID_CREDENTIALS" });

            // NOTE: real password verification belongs in an IAuthService — this scaffold
            // mints tokens for any non-empty credential so the RememberMe wiring is testable
            // end-to-end. Security-hardening-squad owns the credential check.
            var userId = req.Email.ToLowerInvariant();
            var now = clock.GetUtcNow();
            var refreshLifetime = RefreshTokenLifetimes.For(req.RememberMe);

            var refresh = new RefreshToken(
                Token: GenerateOpaqueToken(),
                UserId: userId,
                IssuedAt: now,
                ExpiresAt: now + refreshLifetime,
                RememberMe: req.RememberMe);
            await store.SaveAsync(refresh, ct);

            return Results.Ok(new LoginResponse(
                AccessToken: GenerateOpaqueToken(),
                RefreshToken: refresh.Token,
                AccessTokenExpiresInSeconds: (int)RefreshTokenLifetimes.AccessToken.TotalSeconds,
                RefreshTokenExpiresInSeconds: (int)refreshLifetime.TotalSeconds,
                RememberMe: req.RememberMe));
        })
        .WithName("Login")
        .WithTags("Auth");

        // RM-004: rotation must preserve the original RememberMe window.
        app.MapPost("/api/auth/refresh", async (RefreshRequest req, IRefreshTokenStore store, TimeProvider clock, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.BadRequest(new { error = "INVALID_TOKEN" });

            var existing = await store.FindAsync(req.RefreshToken, ct);
            var now = clock.GetUtcNow();
            if (existing is null || existing.Revoked || existing.ExpiresAt <= now)
                return Results.Unauthorized();

            await store.RevokeAsync(existing.Token, ct);

            var lifetime = RefreshTokenLifetimes.For(existing.RememberMe);
            var rotated = new RefreshToken(
                Token: GenerateOpaqueToken(),
                UserId: existing.UserId,
                IssuedAt: now,
                ExpiresAt: now + lifetime,
                RememberMe: existing.RememberMe);
            await store.SaveAsync(rotated, ct);

            return Results.Ok(new RefreshResponse(
                AccessToken: GenerateOpaqueToken(),
                RefreshToken: rotated.Token,
                AccessTokenExpiresInSeconds: (int)RefreshTokenLifetimes.AccessToken.TotalSeconds,
                RefreshTokenExpiresInSeconds: (int)lifetime.TotalSeconds,
                RememberMe: rotated.RememberMe));
        })
        .WithName("RefreshAuth")
        .WithTags("Auth");

        return app;
    }

    private static string GenerateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
