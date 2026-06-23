namespace TravelAssistant.Agent.Abstractions.Providers;

/// <summary>
/// APP-4 provider adapter base. One interface per provider category. QA records HTTP-level
/// fixtures against implementations. Adapters MUST be idempotent on 5xx retries and MUST NOT
/// leak raw provider JSON to callers — always project into the stable DTOs below.
/// </summary>
public interface IFlightProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<FlightOffer>> SearchAsync(FlightSearch search, CancellationToken ct = default);
}

public interface ILodgingProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<LodgingOffer>> SearchAsync(LodgingSearch search, CancellationToken ct = default);
}

public interface IActivityProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<ActivityOffer>> SearchAsync(ActivitySearch search, CancellationToken ct = default);
}

public sealed record FlightSearch(string OriginIata, string DestinationIata, DateOnly Date, int Travelers);
public sealed record LodgingSearch(string Destination, DateOnly CheckIn, DateOnly CheckOut, int Guests);
public sealed record ActivitySearch(string Destination, DateOnly Date, IReadOnlyList<string>? Preferences = null);

public sealed record FlightOffer(string ProviderRefId, string Origin, string Destination,
    DateTime DepartsAt, DateTime ArrivesAt, string? Carrier, string? FlightNumber, decimal PriceUsd);
public sealed record LodgingOffer(string ProviderRefId, string Name, string Address, decimal NightlyUsd, double? Rating);
public sealed record ActivityOffer(string ProviderRefId, string Title, ActivityKind Kind, int DurationMinutes, decimal? CostUsd);

public sealed class ProviderUnavailableException(string providerId, string message) : Exception($"{providerId}: {message}")
{
    public string ProviderId { get; } = providerId;
}
