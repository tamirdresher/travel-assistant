using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 §6 — audit log writer. JSON Lines via ILogger so Serilog can
// route to whatever sink ops configures. Per §I3 / semgrep
// `login-email-must-be-hashed-in-audit`: never emit raw email, raw password,
// full JWT, full refresh token, Authorization, or Cookie headers.
public interface ILoginAuditLog
{
    void Write(LoginAuditEntry entry);
}

public sealed class LoginAuditLog : ILoginAuditLog
{
    private readonly ILogger<LoginAuditLog> _logger;

    public LoginAuditLog(ILogger<LoginAuditLog> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void Write(LoginAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var json = JsonSerializer.Serialize(entry);
        _logger.LogInformation("LOGIN_AUDIT {Audit}", json);
    }
}

// §6 — emailHash := base64(SHA-256(NFC-lowercase(email))).
public static class LoginHashing
{
    public static string EmailHash(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var normalized = email.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormC);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToBase64String(bytes);
    }

    // §7-E — optional clientIp hashing for GDPR-strict deployments.
    public static string HashClientIp(string clientIp, string dailySalt)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        ArgumentNullException.ThrowIfNull(dailySalt);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(clientIp + "|" + dailySalt));
        return Convert.ToBase64String(bytes);
    }
}
