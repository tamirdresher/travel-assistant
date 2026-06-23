namespace TravelAssistant.Api.Auth;

/// <summary>
/// RM-004 (v2) refresh token row. RM-005 §6 binding schema:
///   * FamilyId         — minted at login; survives rotation; revoked atomically on reuse.
///   * FamilyOriginAt   — original login time; enforces 90d absolute cap on remember-me families.
///   * RememberMe       — read from THIS row on /refresh, never from the request body.
///   * Revoked / RotatedTo — reuse detection: if a revoked row is presented, revoke the family.
/// </summary>
public sealed record RefreshToken(
    string Token,
    string UserId,
    Guid FamilyId,
    DateTimeOffset FamilyOriginAt,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool RememberMe,
    bool Revoked = false,
    string? RotatedTo = null);
