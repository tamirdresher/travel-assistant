namespace TravelAssistant.Data;

public sealed record ChatThread(
    string Id,
    string UserId,
    string Title,
    int Version,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<ChatMessage> Messages);

public sealed record ChatMessage(
    string Id,
    string Role,
    string Content,
    DateTimeOffset Utc,
    IReadOnlyList<string>? ToolCallIds = null);

public sealed record Itinerary(
    string Id,
    string ThreadId,
    string UserId,
    int Version,
    DateTimeOffset CreatedUtc,
    string TripPlanJson);

public sealed record UserRow(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedUtc);

public sealed record BookingRow(
    Guid Id,
    Guid UserId,
    string ItineraryId,
    int ItineraryVersion,
    string ProviderRef,
    string Kind,
    decimal AmountUsd,
    DateTimeOffset CreatedUtc);
