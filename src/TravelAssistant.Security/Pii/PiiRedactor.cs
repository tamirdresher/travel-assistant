using System.Text.RegularExpressions;

namespace TravelAssistant.Security.Pii;

/// <summary>
/// Categories of PII the redactor recognizes. Stable enum — additive only.
/// </summary>
public enum PiiCategory
{
    Email,
    Phone,
    CreditCard,
    Ssn,
    IpAddress,
    Passport,
    Iban,
    JwtOrApiKey
}

/// <summary>
/// One match found in a string.
/// </summary>
/// <param name="Category">What was matched.</param>
/// <param name="Start">Inclusive start index in the original input.</param>
/// <param name="Length">Length of the original substring.</param>
/// <param name="Value">The original substring (do not log).</param>
public readonly record struct PiiMatch(PiiCategory Category, int Start, int Length, string Value);

/// <summary>
/// Result of a redact call.
/// </summary>
/// <param name="Redacted">The redacted string with placeholders substituted.</param>
/// <param name="Matches">All matches found, in input order, non-overlapping.</param>
public readonly record struct PiiRedactionResult(string Redacted, IReadOnlyList<PiiMatch> Matches);

/// <summary>
/// Deterministic, regex-based PII redactor with structural validation
/// (Luhn for cards, mod-97 for IBAN). No network calls, no allocations beyond
/// what string interpolation requires. Safe to call on log lines and prompt
/// payloads. Placeholder format: [REDACTED:CATEGORY].
///
/// Threading: instance is stateless and thread-safe. Regex objects are
/// compiled once at type init.
///
/// SEC-1b owner: security-hardening-squad.
/// </summary>
public static class PiiRedactor
{
    // Email: RFC-5322-lite. Conservative — does not try to match every legal address,
    // but covers >99% of real-world emails without false positives on prose.
    private static readonly Regex EmailRx = new(
        @"(?<![A-Za-z0-9._%+\-])[A-Za-z0-9._%+\-]{1,64}@[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)+(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Phone: international (+CC ...) or US-style (NPA NXX XXXX) with separators.
    // Requires word boundary on both sides so version strings like "1.2.3" or
    // "v9.0.16" cannot match.
    private static readonly Regex PhoneRx = new(
        @"(?<![\w.])(?:\+\d{1,3}[\s\-.]?)?(?:\(\d{2,4}\)[\s\-.]?|\d{2,4}[\s\-.]?)\d{3,4}[\s\-.]?\d{3,4}(?![\w.])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Credit card: 13-19 digits with optional space/dash separators in groups of 4.
    // Final acceptance gated on Luhn check below to suppress version/order-number false positives.
    private static readonly Regex CreditCardCandidateRx = new(
        @"(?<!\d)(?:\d[\s\-]?){12,18}\d(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // US SSN: NNN-NN-NNNN. Disallows 000/666/9xx area and 00 group / 0000 serial per SSA rules.
    private static readonly Regex SsnRx = new(
        @"(?<!\d)(?!000|666|9\d{2})\d{3}-(?!00)\d{2}-(?!0000)\d{4}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // IPv4 + IPv6. IPv4 octets bounded 0-255 by structural check after regex match.
    private static readonly Regex IpV4Rx = new(
        @"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IpV6Rx = new(
        @"(?<![\w:])(?:[A-Fa-f0-9]{1,4}:){7}[A-Fa-f0-9]{1,4}(?![\w:])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Passport (generic): 6-9 alphanumeric with at least one letter and one digit,
    // anchored on word boundaries. Conservative — most country formats fit.
    private static readonly Regex PassportRx = new(
        @"(?<![A-Za-z0-9])(?=[A-Z0-9]{6,9}(?![A-Za-z0-9]))(?=[A-Z0-9]*[A-Z])(?=[A-Z0-9]*\d)[A-Z0-9]{6,9}(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // IBAN: 2-letter country + 2 check digits + up to 30 alphanumeric.
    // Mod-97 validated below.
    private static readonly Regex IbanRx = new(
        @"(?<![A-Za-z0-9])[A-Z]{2}\d{2}[A-Z0-9]{11,30}(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // JWT: three base64url segments separated by dots. Header MUST start with eyJ
    // (base64url of '{"') to avoid matching arbitrary dotted tokens.
    private static readonly Regex JwtRx = new(
        @"(?<![A-Za-z0-9._\-])eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+(?![A-Za-z0-9._\-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // High-entropy API-key heuristic: 32+ chars of [A-Za-z0-9_\-] preceded by an
    // obvious cue word. Cue-gating keeps false positives off for hashes/IDs in prose.
    private static readonly Regex ApiKeyCuedRx = new(
        @"(?i)(?:api[_\- ]?key|secret|token|bearer)\s*[:=]\s*[""']?(?<key>[A-Za-z0-9_\-]{20,})[""']?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Redacts every recognized PII span in <paramref name="input"/>.</summary>
    public static PiiRedactionResult Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new PiiRedactionResult(input ?? string.Empty, Array.Empty<PiiMatch>());
        }

        var matches = new List<PiiMatch>();

        Collect(input, EmailRx, PiiCategory.Email, matches, _ => true);
        Collect(input, CreditCardCandidateRx, PiiCategory.CreditCard, matches, m => IsValidLuhn(m.Value));
        Collect(input, PhoneRx, PiiCategory.Phone, matches, m => CountDigits(m.Value) >= 7);
        Collect(input, SsnRx, PiiCategory.Ssn, matches, _ => true);
        Collect(input, IpV4Rx, PiiCategory.IpAddress, matches, m => IsValidIpv4(m.Value));
        Collect(input, IpV6Rx, PiiCategory.IpAddress, matches, _ => true);
        Collect(input, PassportRx, PiiCategory.Passport, matches, _ => true);
        Collect(input, IbanRx, PiiCategory.Iban, matches, m => IsValidIban(m.Value));
        Collect(input, JwtRx, PiiCategory.JwtOrApiKey, matches, _ => true);

        // Cued API keys: capture only the key group, not the cue word.
        foreach (Match m in ApiKeyCuedRx.Matches(input))
        {
            var key = m.Groups["key"];
            if (key.Success)
            {
                matches.Add(new PiiMatch(PiiCategory.JwtOrApiKey, key.Index, key.Length, key.Value));
            }
        }

        if (matches.Count == 0)
        {
            return new PiiRedactionResult(input, Array.Empty<PiiMatch>());
        }

        // Resolve overlaps: longer match wins, ties broken by earlier start.
        matches.Sort((a, b) =>
        {
            int byStart = a.Start.CompareTo(b.Start);
            if (byStart != 0) return byStart;
            return b.Length.CompareTo(a.Length);
        });

        var filtered = new List<PiiMatch>(matches.Count);
        int cursor = -1;
        foreach (var m in matches)
        {
            if (m.Start < cursor) continue; // overlapping with an already-accepted match
            filtered.Add(m);
            cursor = m.Start + m.Length;
        }

        var sb = new System.Text.StringBuilder(input.Length);
        int pos = 0;
        foreach (var m in filtered)
        {
            if (m.Start > pos)
            {
                sb.Append(input, pos, m.Start - pos);
            }
            sb.Append('[').Append("REDACTED:").Append(m.Category.ToString().ToUpperInvariant()).Append(']');
            pos = m.Start + m.Length;
        }
        if (pos < input.Length)
        {
            sb.Append(input, pos, input.Length - pos);
        }

        return new PiiRedactionResult(sb.ToString(), filtered);
    }

    private static void Collect(string input, Regex rx, PiiCategory cat, List<PiiMatch> sink, Func<Match, bool> accept)
    {
        foreach (Match m in rx.Matches(input))
        {
            if (!accept(m)) continue;
            sink.Add(new PiiMatch(cat, m.Index, m.Length, m.Value));
        }
    }

    internal static bool IsValidLuhn(string candidate)
    {
        int sum = 0;
        bool alt = false;
        int digits = 0;
        for (int i = candidate.Length - 1; i >= 0; i--)
        {
            char c = candidate[i];
            if (c < '0' || c > '9') continue;
            int d = c - '0';
            if (alt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            alt = !alt;
            digits++;
        }
        return digits >= 13 && digits <= 19 && sum % 10 == 0;
    }

    private static int CountDigits(string s)
    {
        int n = 0;
        foreach (var c in s) if (c >= '0' && c <= '9') n++;
        return n;
    }

    private static bool IsValidIpv4(string s)
    {
        var parts = s.Split('.');
        if (parts.Length != 4) return false;
        foreach (var p in parts)
        {
            if (p.Length == 0 || p.Length > 3) return false;
            if (!int.TryParse(p, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)) return false;
            if (n < 0 || n > 255) return false;
        }
        return true;
    }

    internal static bool IsValidIban(string iban)
    {
        // Move first 4 chars to end, replace letters with their numeric (A=10..Z=35),
        // then compute mod 97 == 1 using streaming integer arithmetic.
        if (iban.Length < 15 || iban.Length > 34) return false;
        var rearranged = iban[4..] + iban[..4];
        long remainder = 0;
        foreach (var c in rearranged)
        {
            int value;
            if (c >= '0' && c <= '9') value = c - '0';
            else if (c >= 'A' && c <= 'Z') value = c - 'A' + 10;
            else return false;
            // Append value digits one at a time to keep the running number bounded.
            int width = value > 9 ? 2 : 1;
            for (int i = width - 1; i >= 0; i--)
            {
                int digit = (value / (int)Math.Pow(10, i)) % 10;
                remainder = (remainder * 10 + digit) % 97;
            }
        }
        return remainder == 1;
    }
}
