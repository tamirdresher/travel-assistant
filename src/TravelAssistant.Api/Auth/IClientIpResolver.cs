using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace TravelAssistant.Api.Auth;

// LOGIN-002 — resolve the true client IP through a validated proxy chain.
// Threat-model §6 + login-api §6: clientIp MUST come from RFC 7239 `Forwarded`
// (or X-Forwarded-For fallback) ONLY when the immediate peer is in the
// configured `Auth:TrustedProxyCidrs` allow-list. Otherwise use the peer IP.
//
// Returns (ip, ipTrusted). IpTrusted=true means "we believe this IP correctly
// identifies the client": either the peer IS the client (no proxy chain at all,
// or no trusted proxies configured) OR every hop consumed from the chain was
// trusted. Partial-trust chains return IpTrusted=false plus the best-effort IP.
public interface IClientIpResolver
{
    (string Ip, bool IpTrusted) Resolve(HttpContext ctx);
}

public sealed class RfcForwardedClientIpResolver : IClientIpResolver
{
    private readonly IReadOnlyList<IpCidr> _trusted;

    public RfcForwardedClientIpResolver(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var raw = config.GetSection("Auth:TrustedProxyCidrs").Get<string[]>() ?? Array.Empty<string>();
        var parsed = new List<IpCidr>(raw.Length);
        foreach (var entry in raw)
        {
            if (IpCidr.TryParse(entry, out var cidr)) parsed.Add(cidr);
        }
        _trusted = parsed;
    }

    public (string Ip, bool IpTrusted) Resolve(HttpContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var peer = ctx.Connection.RemoteIpAddress;
        var peerStr = peer?.ToString() ?? "unknown";

        // No trusted proxies configured (default) — the peer IS the client.
        if (_trusted.Count == 0 || peer is null) return (peerStr, true);

        // Peer is not trusted — ignore all forwarding headers, peer IS the client.
        if (!IsTrusted(peer)) return (peerStr, true);

        // Peer is a trusted proxy — try RFC 7239 Forwarded first.
        var forwardedChain = TryParseForwarded(ctx.Request.Headers["Forwarded"].ToString());
        if (forwardedChain.Count > 0)
            return WalkChain(forwardedChain);

        // Fallback: X-Forwarded-For (legacy, leftmost = client).
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(xff))
        {
            var chain = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return WalkChain(chain);
        }

        // Trusted peer but no forwarding headers — peer IS the client.
        return (peerStr, true);
    }

    // Walk hops right-to-left (closest to server first). Consume each trusted
    // hop. First untrusted hop (or end of chain) = the client.
    // IpTrusted = true iff every consumed proxy hop was trusted.
    private (string Ip, bool IpTrusted) WalkChain(IList<string> chain)
    {
        bool allTrusted = true;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var hopStr = StripPort(chain[i]);
            if (!IPAddress.TryParse(hopStr, out var hop))
            {
                // Malformed hop — return the last good value (chain[0] as best effort).
                return (StripPort(chain[0]), false);
            }
            // The "client" is at index 0 (leftmost). All hops to the right are proxies.
            if (i == 0) return (hopStr, allTrusted);
            if (!IsTrusted(hop))
            {
                // First untrusted proxy hop — treat THIS as the client; chain to its left is unverifiable.
                return (hopStr, false);
            }
        }
        return ("unknown", false);
    }

    private bool IsTrusted(IPAddress addr)
    {
        foreach (var c in _trusted) if (c.Contains(addr)) return true;
        return false;
    }

    private static string StripPort(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith('"') && s.EndsWith('"') && s.Length >= 2) s = s[1..^1];
        // IPv6 bracketed: [2001:db8::1]:443
        if (s.StartsWith('['))
        {
            var close = s.IndexOf(']');
            if (close > 0) return s.Substring(1, close - 1);
        }
        // IPv4 with port: 192.0.2.1:443 — only strip if exactly one colon.
        var colons = s.Count(ch => ch == ':');
        if (colons == 1)
        {
            var idx = s.IndexOf(':');
            return s[..idx];
        }
        return s;
    }

    // Minimal RFC 7239 parser: extracts `for=` values across all entries.
    // Returns leftmost-first chain. Skips `for=unknown`.
    internal static List<string> TryParseForwarded(string header)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(header)) return result;
        var entries = header.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var kv = p.Trim();
                if (kv.StartsWith("for=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = kv.Substring(4).Trim();
                    if (string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)) continue;
                    if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
                        value = value[1..^1];
                    result.Add(value);
                    break;
                }
            }
        }
        return result;
    }
}

internal readonly record struct IpCidr(IPAddress Network, int PrefixLength)
{
    public static bool TryParse(string s, out IpCidr cidr)
    {
        cidr = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var slash = s.IndexOf('/');
        if (slash < 0) return false;
        if (!IPAddress.TryParse(s[..slash], out var net)) return false;
        if (!int.TryParse(s[(slash + 1)..], out var prefix)) return false;
        var maxBits = net.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
        if (prefix < 0 || prefix > maxBits) return false;
        cidr = new IpCidr(net, prefix);
        return true;
    }

    public bool Contains(IPAddress addr)
    {
        if (addr.AddressFamily != Network.AddressFamily) return false;
        var a = addr.GetAddressBytes();
        var n = Network.GetAddressBytes();
        int full = PrefixLength / 8;
        int rem = PrefixLength % 8;
        for (int i = 0; i < full; i++) if (a[i] != n[i]) return false;
        if (rem == 0) return true;
        int mask = unchecked((byte)(0xFF << (8 - rem)));
        return (a[full] & mask) == (n[full] & mask);
    }
}
