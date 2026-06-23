using System.Security.Cryptography;
using System.Text;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 §8 #14 — JWTs MUST be EdDSA or RS256. HMAC SHA-256/384/512 is
// rejected. Semgrep `jwt-must-not-use-hmac` blocks Microsoft.IdentityModel
// SecurityAlgorithms.HmacSha* anywhere in src/.
//
// This issuer is intentionally minimal — it produces a JWS-shaped string
// signed by RSA (RS256) using a process-bound key. Real impl will swap for
// a KMS-backed signer with `kid`-based rotation. The wire-shape is what the
// gate cares about today.
public interface IAccessTokenIssuer
{
    AccessToken Issue(string userId, string email);
}

public sealed record AccessToken(string Token, int ExpiresInSeconds);

public sealed class RsaAccessTokenIssuer : IAccessTokenIssuer, IDisposable
{
    private const int LifetimeSeconds = 900; // 15 min
    private const string KeyId = "ta-login-v1";

    private readonly RSA _rsa;

    public RsaAccessTokenIssuer()
    {
        _rsa = RSA.Create(2048);
    }

    public AccessToken Issue(string userId, string email)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(email);

        var now = DateTimeOffset.UtcNow;
        var headerJson = $"{{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"{KeyId}\"}}";
        var payloadJson = $"{{\"sub\":\"{userId}\",\"email\":\"{email}\",\"iat\":{now.ToUnixTimeSeconds()},\"exp\":{now.AddSeconds(LifetimeSeconds).ToUnixTimeSeconds()},\"iss\":\"travel-assistant\"}}";

        var header = Base64Url(Encoding.UTF8.GetBytes(headerJson));
        var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{header}.{payload}";
        var sig = _rsa.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new AccessToken($"{signingInput}.{Base64Url(sig)}", LifetimeSeconds);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _rsa.Dispose();
}
