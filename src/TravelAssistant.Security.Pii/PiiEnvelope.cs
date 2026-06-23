using System.Buffers.Text;
using System.Text;

namespace TravelAssistant.Security.Pii;

/// <summary>
/// Envelope format: <c>v1.{wrappedDek}.{nonce}.{ciphertextPlusTag}</c>.
/// All segments are base64url (no '=' padding). Version is parsed as a literal — bumping the
/// version (e.g. for algorithm rotation) requires a new parser branch, not a silent change.
/// </summary>
internal static class PiiEnvelope
{
    public const string Version = "v1";
    public const int NonceLength = 12;   // AES-GCM standard nonce size
    public const int TagLength = 16;     // AES-GCM standard tag size
    public const int DekLength = 32;     // AES-256

    public static string Format(byte[] wrappedDek, byte[] nonce, byte[] ciphertextAndTag)
    {
        var sb = new StringBuilder(Version.Length + 3 + 256);
        sb.Append(Version).Append('.')
          .Append(Base64UrlEncode(wrappedDek)).Append('.')
          .Append(Base64UrlEncode(nonce)).Append('.')
          .Append(Base64UrlEncode(ciphertextAndTag));
        return sb.ToString();
    }

    public static (byte[] WrappedDek, byte[] Nonce, byte[] CiphertextAndTag) Parse(string envelope)
    {
        ArgumentException.ThrowIfNullOrEmpty(envelope);
        var parts = envelope.Split('.');
        if (parts.Length != 4)
        {
            throw new PiiEnvelopeFormatException(
                $"expected 4 segments separated by '.', got {parts.Length}");
        }

        if (parts[0] != Version)
        {
            throw new PiiEnvelopeFormatException(
                $"unsupported envelope version '{parts[0]}' (expected '{Version}')");
        }

        byte[] wrapped, nonce, ct;
        try
        {
            wrapped = Base64UrlDecode(parts[1]);
            nonce = Base64UrlDecode(parts[2]);
            ct = Base64UrlDecode(parts[3]);
        }
        catch (FormatException ex)
        {
            throw new PiiEnvelopeFormatException("invalid base64url segment: " + ex.Message);
        }

        if (nonce.Length != NonceLength)
        {
            throw new PiiEnvelopeFormatException(
                $"nonce length {nonce.Length} != {NonceLength}");
        }

        if (ct.Length < TagLength)
        {
            throw new PiiEnvelopeFormatException(
                $"ciphertext+tag length {ct.Length} < tag length {TagLength}");
        }

        return (wrapped, nonce, ct);
    }

    internal static string Base64UrlEncode(byte[] bytes)
    {
        var b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static byte[] Base64UrlDecode(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        switch (b64.Length % 4)
        {
            case 2: b64 += "=="; break;
            case 3: b64 += "="; break;
            case 1: throw new FormatException("invalid base64url length");
        }
        return Convert.FromBase64String(b64);
    }
}
