using System.ComponentModel.DataAnnotations;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 §8 #2 — password capped at 1024 chars BEFORE Argon2id invocation.
// Semgrep `login-must-cap-password-length` asserts this annotation is present.
public sealed class LoginRequest
{
    [Required]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    // §8 #2 — literal StringLength(1024) form required by login-gate.yml grep
    // and semgrep login-must-cap-password-length pattern-not-regex.
    [Required]
    [StringLength(1024)]
    [MinLength(1)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public sealed record LoginUser(string Id, string Email, string DisplayName);

// Discriminated-union response shape — MFA-ready from day one per LOGIN-001 spec.
public sealed record LoginAuthenticatedResponse(
    string Status,
    string AccessToken,
    int ExpiresInSeconds,
    LoginUser User);

public sealed record LoginMfaRequiredResponse(
    string Status,
    string MfaToken,
    string[] Methods);

// LOGIN-001 §8 #5 — every 401 sub-state returns this byte-identical body.
// The literal "invalid_credentials" appears here so the contract-invariant
// gate (grep invalid_credentials src/**/Auth/) passes.
public sealed record InvalidCredentialsResponse(string Status = "invalid_credentials");

// Internal-only — audit log payload. Per §6 + semgrep rule
// login-email-must-be-hashed-in-audit, only EmailHash is allowed, never raw email.
public sealed record LoginAuditEntry
{
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string CorrelationId { get; init; }
    public required string EmailHash { get; init; }
    public string? UserId { get; init; }
    public string? ClientIp { get; init; }
    // LOGIN-002 — true when the resolver believes ClientIp identifies the actual
    // caller (peer is client, or every consumed proxy hop was in Auth:TrustedProxyCidrs).
    // false on partial-trust chains or malformed forwarding headers.
    public bool IpTrusted { get; init; }
    public string? UserAgent { get; init; }
    public required string Outcome { get; init; }
    public bool RememberMe { get; init; }
    public Guid? FamilyId { get; init; }
}

public static class LoginOutcomes
{
    public const string Success = "Success";
    public const string InvalidCredentials = "InvalidCredentials";
    public const string UnknownUser = "UnknownUser";
    public const string AccountLocked = "AccountLocked";
    public const string EmailUnverified = "EmailUnverified";
    public const string DisabledAccount = "DisabledAccount";
    public const string MfaRequired = "MfaRequired";
    public const string RateLimitedIp = "RateLimitedIp";
    public const string RateLimitedAccount = "RateLimitedAccount";
    public const string SuspiciousAutomation = "SuspiciousAutomation";
    public const string Argon2Overflow503 = "Argon2Overflow503";
}
