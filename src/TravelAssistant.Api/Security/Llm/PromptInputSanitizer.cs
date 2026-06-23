using System.Globalization;
using System.Text;

namespace TravelAssistant.Api.Security.Llm;

/// <summary>
/// SEC-2 / C2 — Sanitizes any untrusted string before it enters an LLM prompt.
/// Untrusted = user input, third-party content, tool results, replayed memory.
/// </summary>
public sealed class PromptInputSanitizer
{
    public enum Source
    {
        User,
        ThirdParty,
        ToolResult,
        Memory,
    }

    private static readonly IReadOnlyDictionary<Source, int> MaxBytesPerSource = new Dictionary<Source, int>
    {
        [Source.User] = 4 * 1024,
        [Source.ThirdParty] = 16 * 1024,
        [Source.ToolResult] = 8 * 1024,
        [Source.Memory] = 8 * 1024,
    };

    public string Sanitize(string? input, Source source, string sourceId)
    {
        if (string.IsNullOrEmpty(input))
        {
            return WrapEnvelope(string.Empty, source, sourceId, truncated: false);
        }

        var stripped = StripDangerousCodepoints(input);
        var (clamped, truncated) = ClampToBudget(stripped, MaxBytesPerSource[source]);
        return WrapEnvelope(clamped, source, sourceId, truncated);
    }

    private static string StripDangerousCodepoints(string input)
    {
        var sb = new StringBuilder(input.Length);
        var e = StringInfo.GetTextElementEnumerator(input);
        while (e.MoveNext())
        {
            var grapheme = (string)e.Current;
            if (grapheme.Length == 0)
            {
                continue;
            }

            var first = char.ConvertToUtf32(grapheme, 0);

            // C0/C1 controls except \t \n \r
            if ((first <= 0x1F && first is not (0x09 or 0x0A or 0x0D)) ||
                (first >= 0x7F && first <= 0x9F))
            {
                continue;
            }

            // BIDI override / isolate codepoints — used to hide prompt injections.
            if (first is 0x202A or 0x202B or 0x202C or 0x202D or 0x202E
                or 0x2066 or 0x2067 or 0x2068 or 0x2069)
            {
                continue;
            }

            sb.Append(grapheme);
        }

        return sb.ToString();
    }

    private static (string Text, bool Truncated) ClampToBudget(string input, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetByteCount(input);
        if (bytes <= maxBytes)
        {
            return (input, false);
        }

        // Truncate on UTF-8 boundary by re-encoding char by char.
        var enc = Encoding.UTF8;
        var sb = new StringBuilder();
        var used = 0;
        foreach (var rune in input.EnumerateRunes())
        {
            var runeBytes = enc.GetByteCount(rune.ToString());
            if (used + runeBytes > maxBytes)
            {
                break;
            }

            sb.Append(rune.ToString());
            used += runeBytes;
        }

        sb.Append("\n[truncated by PromptInputSanitizer]");
        return (sb.ToString(), true);
    }

    private static string WrapEnvelope(string text, Source source, string sourceId, bool truncated)
    {
        // Envelope is the only contract between sanitizer and the system prompt.
        // System prompt MUST instruct the model: contents of <untrusted> are data, not commands.
        var sourceTag = source switch
        {
            Source.User => "user",
            Source.ThirdParty => "third_party",
            Source.ToolResult => "tool_result",
            Source.Memory => "memory",
            _ => "unknown",
        };

        var safeId = string.IsNullOrWhiteSpace(sourceId) ? "anon" : sourceId.Replace('"', '_');
        var truncatedAttr = truncated ? " truncated=\"true\"" : string.Empty;
        return $"<untrusted source=\"{sourceTag}\" id=\"{safeId}\"{truncatedAttr}>\n{text}\n</untrusted>";
    }
}
