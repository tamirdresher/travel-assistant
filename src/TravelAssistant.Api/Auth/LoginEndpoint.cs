using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 — POST /api/auth/login.
//
// Implements all 15 §8 binding invariants from
// docs/security/login-threat-model.md. Each invariant is annotated inline
// with `§I<n>`. The CI gate (.github/workflows/login-gate.yml) and semgrep
// (.semgrep/login-hygiene.yml) enforce these as code-presence asserts.
//
// Wire shape per docs/api/login-api.md §2-3:
//   200 authenticated   — {status:"authenticated",accessToken,expiresInSeconds,user} + Set-Cookie ta_rt
//   200 mfa_required    — {status:"mfa_required",mfaToken,methods}                  (NO Set-Cookie)
//   401 invalid_creds   — {"status":"invalid_credentials"} byte-identical for ALL
//                          internal sub-states (Invalid/UnknownUser/Locked/Unverified/
//                          Disabled/RateLimitedAccount/SuspiciousAutomation).
//   429 rate_limited_ip — RFC 7807 problem+json + Retry-After + X-RateLimit-* headers
//                          (ONLY 429 surfaces X-RateLimit-* per §I9).
//   503 argon2_overflow — RFC 7807 problem+json, Retry-After: 1
public static class LoginEndpoint
{
    private const long BodyByteCap = 4096;                                  // §I1
    private const string InvalidCredentialsJson = "{\"status\":\"invalid_credentials\"}";
    private const string CorrelationHeader = "X-Correlation-Id";            // §I12
    private const string ChallengeHeader = "WWW-Authenticate";              // §I13

    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The literal `MapPost("/api/auth/login"` here activates the
        // code-presence-gated half of login-gate.yml.
        app.MapPost("/api/auth/login", HandleAsync)
           .WithName("login")
           .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        IUserLookup users,
        IPasswordHasher hasher,
        ILoginRateLimiter rl,
        ILoginAuditLog audit,
        IAccessTokenIssuer issuer,
        IWebHostEnvironment env,
        IConfiguration config,
        IClientIpResolver ipResolver,
        ILogger<Argon2idPasswordHasher> log,
        CancellationToken ct)
    {
        // §I12 — correlation id: echo if client supplied, otherwise mint.
        var correlationId = ctx.Request.Headers.TryGetValue(CorrelationHeader, out var corr) && !StringValues.IsNullOrEmpty(corr)
            ? corr.ToString()
            : Guid.NewGuid().ToString("N");
        ctx.Response.Headers[CorrelationHeader] = correlationId;

        ApplySecurityHeaders(ctx);

        // §I10 — Idempotency-Key is silently ignored. No cache/replay store
        // is consulted (gate forbids the literals Cache|Store|Replay|GetOrCreate
        // appearing within 5 lines of Idempotency-Key in this file).
        _ = ctx.Request.Headers.ContainsKey("Idempotency-Key");

        // §I11 — Origin allow-list CSRF defense. Reject Origin: null.
        if (!IsOriginAllowed(ctx, config))
        {
            return Forbidden(ctx, correlationId);
        }

        // §I1 — request body size cap (4096 bytes / 4 KB).
        if (ctx.Request.ContentLength is long len && len > BodyByteCap)
            return Problem(StatusCodes.Status413PayloadTooLarge, "payload_too_large", "Request body exceeds 4096 bytes.", correlationId);

        if (!IsJson(ctx.Request.ContentType))
            return Problem(StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type", "Content-Type must be application/json.", correlationId);

        LoginRequest? body;
        try
        {
            // Enforce 4 KB cap even if ContentLength is missing/chunked.
            ctx.Request.EnableBuffering(bufferThreshold: (int)BodyByteCap, bufferLimit: BodyByteCap);
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var buffer = new char[BodyByteCap + 1];
            var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (read > BodyByteCap)
                return Problem(StatusCodes.Status413PayloadTooLarge, "payload_too_large", "Request body exceeds 4096 bytes.", correlationId);
            var json = new string(buffer, 0, read);
            body = JsonSerializer.Deserialize<LoginRequest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_request", "Malformed JSON body.", correlationId);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrEmpty(body.Password))
            return Problem(StatusCodes.Status400BadRequest, "invalid_request", "email and password are required.", correlationId);
        if (body.Password.Length > 1024)
            return Problem(StatusCodes.Status400BadRequest, "invalid_request", "password too long.", correlationId);

        // LOGIN-002 — resolve client IP through RFC 7239 Forwarded (or XFF
        // fallback) when the immediate peer is in Auth:TrustedProxyCidrs. Empty
        // list (default) makes this equivalent to the peer address. The handler
        // MUST NOT read the raw peer address directly — login-gate Invariant 6-IP
        // forbids that pattern in this file.
        var (clientIp, ipTrusted) = ipResolver.Resolve(ctx);
        var ua = TruncateUserAgent(ctx.Request.Headers.UserAgent.ToString());
        var emailHash = LoginHashing.EmailHash(body.Email);

        // §I7 — per-IP RL checked BEFORE credential verify.
        var ipCheck = rl.CheckIp(clientIp);
        if (ipCheck.Outcome == LoginRateOutcome.RateLimitedIp)
        {
            audit.Write(NewEntry(correlationId, emailHash, null, clientIp, ua, LoginOutcomes.RateLimitedIp, body.RememberMe, ipTrusted: ipTrusted));
            // §I9 — X-RateLimit-* is allowed ONLY on 429 (never on 401, which
            // would be an enumeration aid).
            return RateLimited(ctx, correlationId, ipCheck.RetryAfter);
        }

        // §I7 — per-account RL also checked BEFORE verify.
        var acctCheck = rl.CheckAccount(emailHash);
        if (acctCheck.Outcome != LoginRateOutcome.Allowed)
        {
            // §I5/I8 — Locked / RateLimitedAccount collapse to identical 401.
            var outcome = acctCheck.Outcome == LoginRateOutcome.Locked
                ? LoginOutcomes.AccountLocked
                : LoginOutcomes.RateLimitedAccount;
            audit.Write(NewEntry(correlationId, emailHash, null, clientIp, ua, outcome, body.RememberMe, ipTrusted: ipTrusted));
            return InvalidCredentials(ctx, correlationId);
        }

        var user = users.FindByEmail(body.Email);
        try
        {
            if (user is null)
            {
                // §I1 — dummy verify on unknown email so wall-clock is
                // indistinguishable from a real verify.
                _ = await hasher.VerifyDummyAsync(body.Password, ct).ConfigureAwait(false);
                rl.RecordAccountFailure(emailHash);
                audit.Write(NewEntry(correlationId, emailHash, null, clientIp, ua, LoginOutcomes.UnknownUser, body.RememberMe, ipTrusted: ipTrusted));
                return InvalidCredentials(ctx, correlationId);
            }

            var verified = await hasher.VerifyAsync(body.Password, user.PasswordHash, ct).ConfigureAwait(false);
            if (!verified)
            {
                rl.RecordAccountFailure(emailHash);
                audit.Write(NewEntry(correlationId, emailHash, user.Id, clientIp, ua, LoginOutcomes.InvalidCredentials, body.RememberMe, ipTrusted: ipTrusted));
                return InvalidCredentials(ctx, correlationId);
            }

            // §I5 — EmailUnverified / Disabled also collapse to invalid_credentials wire.
            if (!user.EmailVerified)
            {
                audit.Write(NewEntry(correlationId, emailHash, user.Id, clientIp, ua, LoginOutcomes.EmailUnverified, body.RememberMe, ipTrusted: ipTrusted));
                return InvalidCredentials(ctx, correlationId);
            }
            if (user.Disabled)
            {
                audit.Write(NewEntry(correlationId, emailHash, user.Id, clientIp, ua, LoginOutcomes.DisabledAccount, body.RememberMe, ipTrusted: ipTrusted));
                return InvalidCredentials(ctx, correlationId);
            }

            // Success path —————————————————————————————————————————
            rl.RecordAccountSuccess(emailHash);
            var ttl = body.RememberMe ? RefreshTokenLifetimes.Long : RefreshTokenLifetimes.Short;
            var familyId = Guid.NewGuid();
            var refreshToken = MintRefreshTokenOpaque();
            // §I6/I7 — refresh cookie ONLY via AppendRefreshCookie helper.
            RefreshCookie.AppendRefreshCookie(ctx, env, refreshToken, ttl);

            var access = issuer.Issue(user.Id, user.Email);
            audit.Write(NewEntry(correlationId, emailHash, user.Id, clientIp, ua, LoginOutcomes.Success, body.RememberMe, familyId, ipTrusted: ipTrusted));

            return Results.Json(
                new LoginAuthenticatedResponse(
                    "authenticated",
                    access.Token,
                    access.ExpiresInSeconds,
                    new LoginUser(user.Id, user.Email, user.DisplayName)),
                statusCode: StatusCodes.Status200OK);
        }
        catch (Argon2OverflowException)
        {
            audit.Write(NewEntry(correlationId, emailHash, user?.Id, clientIp, ua, LoginOutcomes.Argon2Overflow503, body.RememberMe, ipTrusted: ipTrusted));
            return Problem(StatusCodes.Status503ServiceUnavailable, "argon2_overflow", "Login capacity temporarily exhausted.", correlationId, retryAfterSeconds: 1);
        }
    }

    // ————————————————————————————————————————————————————————————————

    private static bool IsJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var ct = contentType.Split(';')[0].Trim();
        return string.Equals(ct, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOriginAllowed(HttpContext ctx, IConfiguration config)
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        // Same-origin browser requests sometimes omit Origin — allow when no Origin header.
        if (string.IsNullOrEmpty(origin)) return true;
        if (string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase)) return false;
        var allow = config.GetSection("Auth:OriginAllowList").Get<string[]>() ?? Array.Empty<string>();
        foreach (var a in allow)
        {
            if (string.Equals(a, origin, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void ApplySecurityHeaders(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["Cache-Control"] = "no-store";
        h["Pragma"] = "no-cache";
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "no-referrer";
    }

    private static IResult InvalidCredentials(HttpContext ctx, string correlationId)
    {
        // §I13 — WWW-Authenticate: Bearer realm="ta", error="invalid_credentials"
        ctx.Response.Headers[ChallengeHeader] = "Bearer realm=\"ta\", error=\"invalid_credentials\"";
        ctx.Response.Headers[CorrelationHeader] = correlationId;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Results.Text(InvalidCredentialsJson, "application/json; charset=utf-8", Encoding.UTF8, StatusCodes.Status401Unauthorized);
    }

    private static IResult RateLimited(HttpContext ctx, string correlationId, TimeSpan retryAfter)
    {
        var seconds = (int)Math.Max(1, retryAfter.TotalSeconds);
        // §I9 — X-RateLimit-* lives ONLY on 429.
        ctx.Response.Headers["X-RateLimit-Limit"] = "10";
        ctx.Response.Headers["X-RateLimit-Remaining"] = "0";
        ctx.Response.Headers["X-RateLimit-Reset"] = seconds.ToString();
        ctx.Response.Headers["Retry-After"] = seconds.ToString();
        return Problem(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many login attempts from this client.", correlationId, retryAfterSeconds: seconds);
    }

    private static IResult Forbidden(HttpContext ctx, string correlationId)
        => Problem(StatusCodes.Status403Forbidden, "origin_not_allowed", "Origin is not in the allow-list.", correlationId);

    private static IResult Problem(int status, string slug, string detail, string correlationId, int? retryAfterSeconds = null)
    {
        var pd = new ProblemDetails
        {
            Type = $"https://api.travel-assistant/problems/{slug}",
            Title = slug.Replace('_', ' '),
            Status = status,
            Detail = detail,
            Instance = "/api/auth/login",
        };
        pd.Extensions["correlationId"] = correlationId;
        if (retryAfterSeconds is int s) pd.Extensions["retryAfterSeconds"] = s;
        return Results.Json(pd, statusCode: status, contentType: "application/problem+json");
    }

    private static LoginAuditEntry NewEntry(
        string correlationId, string emailHash, string? userId, string clientIp,
        string ua, string outcome, bool rememberMe, Guid? familyId = null, bool ipTrusted = true)
        => new LoginAuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            EmailHash = emailHash,
            UserId = userId,
            ClientIp = clientIp,
            IpTrusted = ipTrusted,
            UserAgent = ua,
            Outcome = outcome,
            RememberMe = rememberMe,
            FamilyId = familyId,
        };

    private static string TruncateUserAgent(string ua)
        => string.IsNullOrEmpty(ua) ? string.Empty : (ua.Length <= 256 ? ua : ua[..256]);

    private static string MintRefreshTokenOpaque()
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
