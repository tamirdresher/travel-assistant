using System.Net;
using System.Net.Sockets;

namespace TravelAssistant.Api.Security;

/// <summary>
/// SEC-3 — DelegatingHandler that enforces the outbound URL allowlist
/// + SSRF guard. Wire this into any HttpClient used to fetch a URL that
/// derives from user input, third-party content, or LLM tool arguments.
/// </summary>
public sealed class SsrfGuardingHttpHandler : DelegatingHandler
{
    private readonly IReadOnlyCollection<string> _allowedHosts;
    private readonly IReadOnlyCollection<string> _allowedHostSuffixes;
    private readonly bool _isLocalhostAllowed;
    private readonly ILogger<SsrfGuardingHttpHandler> _logger;

    public SsrfGuardingHttpHandler(
        IEnumerable<string> allowlist,
        bool isLocalhostAllowed,
        ILogger<SsrfGuardingHttpHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(logger);

        var hosts = new List<string>();
        var suffixes = new List<string>();
        foreach (var entry in allowlist)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var lower = entry.Trim().ToLowerInvariant();
            if (lower.StartsWith("*.", StringComparison.Ordinal))
            {
                suffixes.Add(lower[1..]); // ".example.com"
            }
            else
            {
                hosts.Add(lower);
            }
        }

        _allowedHosts = hosts;
        _allowedHostSuffixes = suffixes;
        _isLocalhostAllowed = isLocalhostAllowed;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri
            ?? throw new InvalidOperationException("SSRF guard: request URI is null.");

        Validate(uri);
        await ValidateResolvedAddressesAsync(uri, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void Validate(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            Block(uri.Host, "scheme is not https");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            Block(uri.Host, "userinfo in URL");
        }

        var host = uri.Host.ToLowerInvariant();

        if (IsIpLiteral(host))
        {
            Block(host, "IP literal host not allowed");
        }

        var allowed = _allowedHosts.Contains(host)
            || _allowedHostSuffixes.Any(s => host.EndsWith(s, StringComparison.Ordinal));

        if (!allowed)
        {
            Block(host, "host not in allowlist");
        }
    }

    private async Task ValidateResolvedAddressesAsync(Uri uri, CancellationToken ct)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, ct).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            Block(uri.Host, $"DNS resolution failed: {ex.SocketErrorCode}");
            return; // unreachable
        }

        if (addresses.Length == 0)
        {
            Block(uri.Host, "DNS returned no addresses");
        }

        foreach (var ip in addresses)
        {
            if (IsBlocked(ip))
            {
                Block(uri.Host, "resolved IP in blocked range");
            }
        }
    }

    private static bool IsIpLiteral(string host)
        => IPAddress.TryParse(host.TrimStart('[').TrimEnd(']'), out _);

    private bool IsBlocked(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return !_isLocalhostAllowed;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            // 169.254.0.0/16 — link-local incl. IMDS
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // RFC1918
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // CGNAT 100.64.0.0/10
            if (bytes[0] == 100 && (bytes[1] & 0xC0) == 0x40) return true;

            // Multicast 224.0.0.0/4
            if ((bytes[0] & 0xF0) == 0xE0) return true;

            // Reserved 240.0.0.0/4
            if ((bytes[0] & 0xF0) == 0xF0) return true;

            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal) return true;
            if (ip.IsIPv6SiteLocal) return true;
            if (ip.IsIPv6Multicast) return true;
            if (ip.IsIPv4MappedToIPv6) return IsBlocked(ip.MapToIPv4());

            var bytes = ip.GetAddressBytes();
            // ULA fc00::/7
            if ((bytes[0] & 0xFE) == 0xFC) return true;
        }

        return false;
    }

    private void Block(string host, string reason)
    {
        _logger.LogWarning("ssrf.blocked host={Host} reason={Reason}", host, reason);
        throw new SsrfBlockedException(host, reason);
    }
}

public sealed class SsrfBlockedException : Exception
{
    public string Host { get; }
    public string Reason { get; }

    public SsrfBlockedException(string host, string reason)
        : base($"Blocked outbound request to '{host}': {reason}.")
    {
        Host = host;
        Reason = reason;
    }
}
