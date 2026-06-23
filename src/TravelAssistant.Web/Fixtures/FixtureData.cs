// APP-11 — In-process deterministic fixture data for axe runner harness.
// No real PII, geo, or prices. mulberry32 seeded so axe runs are reproducible.
// Contract: docs/design/fixtures/axe-fixture-contract.md (XD-6c).
using System.Globalization;

namespace TravelAssistant.Web.Fixtures;

public enum FixtureState
{
    Idle,
    Loading,
    Empty,
    Streaming,
    PendingPatch,
    AppliedPatch,
    ModalDeferred,
    TurnCancelled,
    TurnError,
    Error,
}

public static class FixtureStates
{
    public static readonly IReadOnlyList<string> Canonical = new[]
    {
        "idle", "loading", "empty", "streaming", "pending-patch",
        "applied-patch", "modal-deferred", "turn-cancelled", "turn-error", "error",
    };

    public static bool TryParse(string? value, out FixtureState state)
    {
        state = FixtureState.Idle;
        if (string.IsNullOrWhiteSpace(value)) return true;
        switch (value.Trim().ToLowerInvariant())
        {
            case "idle": state = FixtureState.Idle; return true;
            case "loading": state = FixtureState.Loading; return true;
            case "empty": state = FixtureState.Empty; return true;
            case "streaming": state = FixtureState.Streaming; return true;
            case "pending-patch": state = FixtureState.PendingPatch; return true;
            case "applied-patch": state = FixtureState.AppliedPatch; return true;
            case "modal-deferred": state = FixtureState.ModalDeferred; return true;
            case "turn-cancelled": state = FixtureState.TurnCancelled; return true;
            case "turn-error": state = FixtureState.TurnError; return true;
            case "error": state = FixtureState.Error; return true;
            default: return false;
        }
    }

    public static string ToSlug(FixtureState s) => s switch
    {
        FixtureState.Idle => "idle",
        FixtureState.Loading => "loading",
        FixtureState.Empty => "empty",
        FixtureState.Streaming => "streaming",
        FixtureState.PendingPatch => "pending-patch",
        FixtureState.AppliedPatch => "applied-patch",
        FixtureState.ModalDeferred => "modal-deferred",
        FixtureState.TurnCancelled => "turn-cancelled",
        FixtureState.TurnError => "turn-error",
        FixtureState.Error => "error",
        _ => "idle",
    };
}

/// <summary>Deterministic PRNG (mulberry32) so a given seed yields the same fixture content every render.</summary>
public sealed class Mulberry32
{
    private uint _state;
    public Mulberry32(int seed) => _state = unchecked((uint)seed);

    public uint NextUInt32()
    {
        unchecked
        {
            _state += 0x6D2B79F5u;
            uint z = _state;
            z = (z ^ (z >> 15)) * (z | 1u);
            z ^= z + (z ^ (z >> 7)) * (z | 61u);
            return z ^ (z >> 14);
        }
    }

    public int Next(int maxExclusive) =>
        maxExclusive <= 0 ? 0 : (int)(NextUInt32() % (uint)maxExclusive);

    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items[Next(items.Count)];
    }
}

public sealed record FixtureMessage(
    string Role,           // user | assistant | system
    string Text,
    string PatchStatus);   // pending | applied | none

public static class FixtureData
{
    // Synthetic, non-PII tokens. Cities are fictional placeholders.
    private static readonly string[] FakeCities = { "Aldermoor", "Brindleby", "Carrowfen", "Deepholm", "Everstead" };
    private static readonly string[] FakeActivities = { "museum visit", "market stroll", "coastal walk", "garden tour", "evening concert" };
    private static readonly string[] StreamingTokens = { "Planning", "your", "trip", "with", "options", "from" };

    public static IReadOnlyList<FixtureMessage> ForState(FixtureState state, int seed)
    {
        var rng = new Mulberry32(seed);
        return state switch
        {
            FixtureState.Idle => Array.Empty<FixtureMessage>(),
            FixtureState.Empty => Array.Empty<FixtureMessage>(),
            FixtureState.Loading => new[]
            {
                new FixtureMessage("user", "Plan a 3-day visit to " + rng.Pick(FakeCities), "none"),
            },
            FixtureState.Streaming => BuildStreaming(rng),
            FixtureState.PendingPatch => BuildPendingPatch(rng),
            FixtureState.AppliedPatch => BuildAppliedPatch(rng),
            FixtureState.ModalDeferred => new[]
            {
                new FixtureMessage("user", "Help me pick a hotel near " + rng.Pick(FakeCities), "none"),
                new FixtureMessage("assistant", "Found a few options matching your preferences.", "none"),
            },
            FixtureState.TurnCancelled => new[]
            {
                new FixtureMessage("user", "Outline tomorrow's itinerary", "none"),
                new FixtureMessage("system", "Turn cancelled.", "none"),
            },
            FixtureState.TurnError => new[]
            {
                new FixtureMessage("user", "Show flights to " + rng.Pick(FakeCities), "none"),
                new FixtureMessage("system", "An error occurred while planning.", "none"),
            },
            FixtureState.Error => Array.Empty<FixtureMessage>(),
            _ => Array.Empty<FixtureMessage>(),
        };
    }

    private static FixtureMessage[] BuildStreaming(Mulberry32 rng)
    {
        int tokenCount = 3 + rng.Next(4); // 3..6 partial tokens
        var partials = string.Join(' ', Enumerable.Range(0, tokenCount).Select(i => StreamingTokens[i % StreamingTokens.Length]));
        return new[]
        {
            new FixtureMessage("user", "What should we do in " + rng.Pick(FakeCities) + "?", "none"),
            new FixtureMessage("assistant", partials, "none"),
        };
    }

    private static FixtureMessage[] BuildPendingPatch(Mulberry32 rng)
    {
        var msgs = new List<FixtureMessage>(6);
        for (int i = 0; i < 6; i++)
        {
            string status = (i % 2 == 0) ? "pending" : "applied";
            string activity = rng.Pick(FakeActivities);
            msgs.Add(new FixtureMessage("assistant", $"Suggestion {i + 1}: {activity}", status));
        }
        return msgs.ToArray();
    }

    private static FixtureMessage[] BuildAppliedPatch(Mulberry32 rng)
    {
        // >20 blocks to trigger virtualization regression guard.
        var msgs = new List<FixtureMessage>(22);
        for (int i = 0; i < 22; i++)
        {
            string activity = rng.Pick(FakeActivities);
            string city = rng.Pick(FakeCities);
            msgs.Add(new FixtureMessage("assistant", $"Day {i + 1} in {city}: {activity}", "applied"));
        }
        return msgs.ToArray();
    }

    public static string FormatSeed(int seed) => seed.ToString(CultureInfo.InvariantCulture);
}
