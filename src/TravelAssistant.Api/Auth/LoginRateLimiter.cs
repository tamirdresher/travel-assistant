using Microsoft.Extensions.Caching.Memory;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 §5 + §8 #15 — two-layer rate limiting.
//
// IP partition key:      "login:ip:{ip}"        — 10/15min, fail-closed-503.
// Account partition key: "login:account:{hash}" — 5/15min,  fail-closed-503,
//                                                  increments on UnknownUser too.
// Lockout:               "login:lock:{hash}"    — 5 fails/15min triggers a
//                                                  15-min hard lock; identical
//                                                  401 wire to wrong-password.
//
// The literals `login:account:` and `login:ip:` are load-bearing for the gate
// regex `'login:account:|account[-_]?partition'`.
public interface ILoginRateLimiter
{
    LoginRateDecision CheckIp(string clientIp);
    LoginRateDecision CheckAccount(string emailHash);
    void RecordAccountFailure(string emailHash);
    void RecordAccountSuccess(string emailHash);
}

public enum LoginRateOutcome
{
    Allowed,
    RateLimitedIp,
    RateLimitedAccount,
    Locked,
}

public sealed record LoginRateDecision(LoginRateOutcome Outcome, TimeSpan RetryAfter);

public sealed class LoginRateLimiter : ILoginRateLimiter
{
    private const int IpAttemptsPerWindow = 10;
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(15);

    private const int AccountFailsPerWindow = 5;
    private static readonly TimeSpan AccountWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public LoginRateLimiter(IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public LoginRateDecision CheckIp(string clientIp)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        var key = $"login:ip:{clientIp}";
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = IpWindow;
            return new Counter();
        })!.Increment();
        if (count > IpAttemptsPerWindow)
            return new LoginRateDecision(LoginRateOutcome.RateLimitedIp, IpWindow);
        return new LoginRateDecision(LoginRateOutcome.Allowed, TimeSpan.Zero);
    }

    public LoginRateDecision CheckAccount(string emailHash)
    {
        ArgumentNullException.ThrowIfNull(emailHash);
        if (_cache.TryGetValue($"login:lock:{emailHash}", out _))
            return new LoginRateDecision(LoginRateOutcome.Locked, LockDuration);
        var key = $"login:account:{emailHash}";
        if (_cache.TryGetValue(key, out Counter? c) && c is not null && c.Value >= AccountFailsPerWindow)
            return new LoginRateDecision(LoginRateOutcome.RateLimitedAccount, AccountWindow);
        return new LoginRateDecision(LoginRateOutcome.Allowed, TimeSpan.Zero);
    }

    public void RecordAccountFailure(string emailHash)
    {
        ArgumentNullException.ThrowIfNull(emailHash);
        var key = $"login:account:{emailHash}";
        var counter = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = AccountWindow;
            return new Counter();
        })!;
        var next = counter.Increment();
        if (next >= AccountFailsPerWindow)
        {
            _cache.Set($"login:lock:{emailHash}", true, LockDuration);
        }
    }

    public void RecordAccountSuccess(string emailHash)
    {
        ArgumentNullException.ThrowIfNull(emailHash);
        _cache.Remove($"login:account:{emailHash}");
        _cache.Remove($"login:lock:{emailHash}");
    }

    private sealed class Counter
    {
        private int _value;
        public int Value => _value;
        public int Increment() => Interlocked.Increment(ref _value);
    }
}
