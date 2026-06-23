namespace TravelAssistant.Data;

/// <summary>APP-5: Cosmos-backed store for chat threads. Versioned documents.</summary>
public interface IChatThreadStore
{
    Task<ChatThread?> GetAsync(string threadId, int? version = null, CancellationToken ct = default);
    Task<ChatThread> UpsertAsync(ChatThread thread, CancellationToken ct = default);
    IAsyncEnumerable<ChatThread> ListByUserAsync(string userId, CancellationToken ct = default);
    Task DeleteAsync(string threadId, CancellationToken ct = default);
}

/// <summary>APP-5: Cosmos-backed store for versioned itineraries.</summary>
public interface IItineraryStore
{
    Task<Itinerary?> GetLatestAsync(string itineraryId, CancellationToken ct = default);
    Task<Itinerary?> GetVersionAsync(string itineraryId, int version, CancellationToken ct = default);
    Task<Itinerary> AppendVersionAsync(Itinerary itinerary, CancellationToken ct = default);
    IAsyncEnumerable<Itinerary> ListByThreadAsync(string threadId, CancellationToken ct = default);
}
