using TravelAssistant.Agent.Abstractions;
using TravelAssistant.Agent.Abstractions.Providers;

namespace TravelAssistant.Agent.Abstractions.Providers;

/// <summary>
/// Deterministic in-memory providers used when env var <c>TRAVEL_PROVIDERS=InMemory</c> is set.
/// Required by APP-1 so QA E2E can run without hitting real APIs. Output is stable for fixture recording.
/// </summary>
public sealed class InMemoryFlightProvider : IFlightProvider
{
    public string ProviderId => "InMemory";
    public Task<IReadOnlyList<FlightOffer>> SearchAsync(FlightSearch s, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(s);
        var depart = s.Date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        var arrive = depart.AddHours(3);
        IReadOnlyList<FlightOffer> r =
        [
            new FlightOffer($"inmem-{s.OriginIata}-{s.DestinationIata}-{s.Date:yyyyMMdd}",
                s.OriginIata, s.DestinationIata, depart, arrive, "InMemory Air", "IM100", 199m * s.Travelers)
        ];
        return Task.FromResult(r);
    }
}

public sealed class InMemoryLodgingProvider : ILodgingProvider
{
    public string ProviderId => "InMemory";
    public Task<IReadOnlyList<LodgingOffer>> SearchAsync(LodgingSearch s, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(s);
        IReadOnlyList<LodgingOffer> r =
        [
            new LodgingOffer($"inmem-stay-{s.Destination}-{s.CheckIn:yyyyMMdd}",
                $"InMemory Hotel {s.Destination}", $"1 Test St, {s.Destination}", 120m, 4.2)
        ];
        return Task.FromResult(r);
    }
}

public sealed class InMemoryActivityProvider : IActivityProvider
{
    public string ProviderId => "InMemory";
    public Task<IReadOnlyList<ActivityOffer>> SearchAsync(ActivitySearch s, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(s);
        IReadOnlyList<ActivityOffer> r =
        [
            new ActivityOffer($"inmem-act-sight-{s.Date:yyyyMMdd}", $"Walking tour of {s.Destination}", ActivityKind.Sight, 120, 25m),
            new ActivityOffer($"inmem-act-meal-{s.Date:yyyyMMdd}", $"Dinner in {s.Destination}", ActivityKind.Meal, 90, 40m),
        ];
        return Task.FromResult(r);
    }
}
