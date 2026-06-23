using System.Text.Json.Serialization;

namespace TravelAssistant.Agent.Abstractions;

/// <summary>
/// Travel agent contract. APP-2 deliverable. Stable wire surface — QA records fixtures against this.
/// All implementations MUST return responses that validate against <c>Schemas/itinerary.schema.json</c>.
/// </summary>
public interface ITravelAgent
{
    /// <summary>Generate an initial itinerary for the trip request.</summary>
    Task<TripPlan> PlanTripAsync(TripRequest request, CancellationToken ct = default);

    /// <summary>Apply a refinement (cheaper, more walkable, add a day, etc.) producing a new plan.</summary>
    Task<TripPlan> RefineTripAsync(TripPlan current, string refinement, CancellationToken ct = default);

    /// <summary>Explain why a specific activity or flight was chosen. Grounded in retrieval or marked pending.</summary>
    Task<ChoiceExplanation> ExplainChoiceAsync(TripPlan plan, string activityId, CancellationToken ct = default);

    /// <summary>Stream partial plan updates as the agent constructs the itinerary.</summary>
    IAsyncEnumerable<TripPlanDelta> StreamPlanAsync(TripRequest request, CancellationToken ct = default);
}

public sealed record TripRequest(
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("start")] DateOnly Start,
    [property: JsonPropertyName("end")] DateOnly End,
    [property: JsonPropertyName("travelers")] int Travelers = 1,
    [property: JsonPropertyName("budgetUsd")] decimal? BudgetUsd = null,
    [property: JsonPropertyName("preferences")] IReadOnlyList<string>? Preferences = null,
    [property: JsonPropertyName("originIata")] string? OriginIata = null);

public sealed record TripPlan(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("start")] DateOnly Start,
    [property: JsonPropertyName("end")] DateOnly End,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("totalCost")] decimal TotalCost,
    [property: JsonPropertyName("flights")] IReadOnlyList<Flight> Flights,
    [property: JsonPropertyName("days")] IReadOnlyList<TripDay> Days,
    [property: JsonPropertyName("provenance")] IReadOnlyList<SourceRef> Provenance);

public sealed record TripDay(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("dayNumber")] int DayNumber,
    [property: JsonPropertyName("activities")] IReadOnlyList<Activity> Activities);

public sealed record Activity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] ActivityKind Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("startsAt")] DateTime StartsAt,
    [property: JsonPropertyName("durationMinutes")] int DurationMinutes,
    [property: JsonPropertyName("costUsd")] decimal? CostUsd,
    [property: JsonPropertyName("locationName")] string? LocationName = null,
    [property: JsonPropertyName("status")] PlanItemStatus Status = PlanItemStatus.Grounded,
    [property: JsonPropertyName("sources")] IReadOnlyList<SourceRef>? Sources = null);

public sealed record Flight(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("departsAt")] DateTime DepartsAt,
    [property: JsonPropertyName("arrivesAt")] DateTime ArrivesAt,
    [property: JsonPropertyName("carrier")] string? Carrier = null,
    [property: JsonPropertyName("flightNumber")] string? FlightNumber = null,
    [property: JsonPropertyName("priceUsd")] decimal? PriceUsd = null,
    [property: JsonPropertyName("status")] PlanItemStatus Status = PlanItemStatus.Grounded,
    [property: JsonPropertyName("sources")] IReadOnlyList<SourceRef>? Sources = null);

public sealed record SourceRef(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("refId")] string RefId,
    [property: JsonPropertyName("retrievedAt")] DateTime RetrievedAt,
    [property: JsonPropertyName("url")] string? Url = null);

public sealed record ChoiceExplanation(
    [property: JsonPropertyName("activityId")] string ActivityId,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("alternativesConsidered")] IReadOnlyList<string> AlternativesConsidered,
    [property: JsonPropertyName("sources")] IReadOnlyList<SourceRef> Sources);

public sealed record TripPlanDelta(
    [property: JsonPropertyName("kind")] DeltaKind Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("value")] object? Value);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivityKind { Meal, Sight, Transit, Lodging, Free, Booking, Other }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanItemStatus { Grounded, Pending, Stale }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeltaKind { Add, Replace, Remove, Annotate }
