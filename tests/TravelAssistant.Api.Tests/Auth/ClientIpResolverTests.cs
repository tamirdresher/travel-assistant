using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TravelAssistant.Api.Auth;
using Xunit;

namespace TravelAssistant.Api.Tests.Auth;

// LOGIN-002 — RfcForwardedClientIpResolver unit tests.
// Validates RFC 7239 Forwarded parsing, X-Forwarded-For fallback, trusted-proxy
// allow-list semantics, and IpTrusted flag correctness.
public sealed class ClientIpResolverTests
{
    private static IClientIpResolver NewResolver(params string[] trustedCidrs)
    {
        var data = new Dictionary<string, string?>();
        for (int i = 0; i < trustedCidrs.Length; i++)
            data[$"Auth:TrustedProxyCidrs:{i}"] = trustedCidrs[i];
        var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        return new RfcForwardedClientIpResolver(config);
    }

    private static DefaultHttpContext CtxWith(string peerIp, (string Name, string Value)[]? headers = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(peerIp);
        if (headers != null)
        {
            foreach (var (n, v) in headers) ctx.Request.Headers[n] = v;
        }
        return ctx;
    }

    [Fact]
    public void Peer_only_no_trusted_proxies_returns_peer_trusted()
    {
        var r = NewResolver();
        var ctx = CtxWith("203.0.113.45");
        var (ip, trusted) = r.Resolve(ctx);
        Assert.Equal("203.0.113.45", ip);
        Assert.True(trusted);
    }

    [Fact]
    public void Untrusted_peer_ignores_forwarded_headers()
    {
        var r = NewResolver("10.0.0.0/8");
        var ctx = CtxWith("203.0.113.45", new[]
        {
            ("Forwarded", "for=192.0.2.1"),
            ("X-Forwarded-For", "192.0.2.1"),
        });
        var (ip, trusted) = r.Resolve(ctx);
        Assert.Equal("203.0.113.45", ip);
        Assert.True(trusted);
    }

    [Fact]
    public void Trusted_peer_with_forwarded_returns_chain_client_trusted()
    {
        var r = NewResolver("10.0.0.0/8");
        var ctx = CtxWith("10.1.2.3", new[]
        {
            ("Forwarded", "for=192.0.2.43"),
        });
        var (ip, trusted) = r.Resolve(ctx);
        Assert.Equal("192.0.2.43", ip);
        Assert.True(trusted);
    }

    [Fact]
    public void Trusted_peer_with_xff_fallback_returns_leftmost_trusted()
    {
        var r = NewResolver("10.0.0.0/8");
        var ctx = CtxWith("10.1.2.3", new[]
        {
            ("X-Forwarded-For", "198.51.100.7, 10.4.5.6"),
        });
        var (ip, trusted) = r.Resolve(ctx);
        Assert.Equal("198.51.100.7", ip);
        Assert.True(trusted);
    }

    [Fact]
    public void Forwarded_takes_precedence_over_xff()
    {
        var r = NewResolver("10.0.0.0/8");
        var ctx = CtxWith("10.1.2.3", new[]
        {
            ("Forwarded", "for=192.0.2.43"),
            ("X-Forwarded-For", "198.51.100.7"),
        });
        var (ip, _) = r.Resolve(ctx);
        Assert.Equal("192.0.2.43", ip);
    }

    [Fact]
    public void Trusted_peer_with_untrusted_intermediate_returns_intermediate_untrusted()
    {
        // Chain in XFF (leftmost = original client): [192.0.2.43, 203.0.113.99, 10.4.5.6]
        // Walk right-to-left: 10.4.5.6 trusted (peer), 203.0.113.99 NOT trusted → that's the
        // "client" we can verify, IpTrusted=false because chain to its left is unverifiable.
        var r = NewResolver("10.0.0.0/8");
        var ctx = CtxWith("10.1.2.3", new[]
        {
            ("X-Forwarded-For", "192.0.2.43, 203.0.113.99, 10.4.5.6"),
        });
        var (ip, trusted) = r.Resolve(ctx);
        Assert.Equal("203.0.113.99", ip);
        Assert.False(trusted);
    }

    [Fact]
    public void Forwarded_with_quoted_ipv6_and_port_strips_brackets_and_port()
    {
        var chain = RfcForwardedClientIpResolver.TryParseForwarded(
            "for=\"[2001:db8::1]:443\";proto=https");
        Assert.Single(chain);
        Assert.Equal("[2001:db8::1]:443", chain[0]);
    }

    [Fact]
    public void Forwarded_with_multiple_entries_returns_leftmost_first()
    {
        var chain = RfcForwardedClientIpResolver.TryParseForwarded(
            "for=192.0.2.43, for=198.51.100.17;by=10.0.0.1");
        Assert.Equal(2, chain.Count);
        Assert.Equal("192.0.2.43", chain[0]);
        Assert.Equal("198.51.100.17", chain[1]);
    }

    [Fact]
    public void Forwarded_unknown_is_skipped()
    {
        var chain = RfcForwardedClientIpResolver.TryParseForwarded("for=unknown, for=192.0.2.43");
        Assert.Single(chain);
        Assert.Equal("192.0.2.43", chain[0]);
    }

    [Fact]
    public void Malformed_forwarded_falls_back_safely_with_untrusted()
    {
        var r = NewResolver("10.0.0.0/8");
        var ctx = CtxWith("10.1.2.3", new[]
        {
            ("Forwarded", "for=not-an-ip"),
        });
        var (_, trusted) = r.Resolve(ctx);
        Assert.False(trusted);
    }

    [Fact]
    public void Ipv6_cidr_matching_works()
    {
        var r = NewResolver("2001:db8::/32");
        var ctx = CtxWith("2001:db8:abcd::1", new[]
        {
            ("Forwarded", "for=\"[2001:4860:4860::8888]\""),
        });
        var (ip, trusted) = r.Resolve(ctx);
        Assert.Equal("2001:4860:4860::8888", ip);
        Assert.True(trusted);
    }
}
