namespace TravelAssistant.Api.Auth;

/// <summary>
/// Persisted refresh token row. The <see cref="RememberMe"/> flag is preserved across
/// rotation so a long-lived session stays long-lived until the user explicitly logs out
/// (RM-004 contract).
/// </summary>
public sealed record RefreshToken(
    string Token,
    string UserId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool RememberMe,
    bool Revoked = false);
