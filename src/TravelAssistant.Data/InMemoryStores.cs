using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace TravelAssistant.Data;

public sealed class InMemoryChatThreadStore : IChatThreadStore
{
    private readonly ConcurrentDictionary<string, List<ChatThread>> _byId = new();

    public Task<ChatThread?> GetAsync(string threadId, int? version = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(threadId);
        if (!_byId.TryGetValue(threadId, out var versions))
        {
            return Task.FromResult<ChatThread?>(null);
        }
        var match = version is null
            ? versions[^1]
            : versions.FirstOrDefault(v => v.Version == version.Value);
        return Task.FromResult<ChatThread?>(match);
    }

    public Task<ChatThread> UpsertAsync(ChatThread thread, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(thread);
        var list = _byId.GetOrAdd(thread.Id, _ => new List<ChatThread>());
        lock (list)
        {
            var next = thread with { Version = list.Count + 1, UpdatedUtc = DateTimeOffset.UtcNow };
            list.Add(next);
            return Task.FromResult(next);
        }
    }

    public async IAsyncEnumerable<ChatThread> ListByUserAsync(string userId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        foreach (var versions in _byId.Values)
        {
            ct.ThrowIfCancellationRequested();
            var latest = versions[^1];
            if (latest.UserId == userId)
            {
                yield return latest;
            }
        }
        await Task.CompletedTask;
    }

    public Task DeleteAsync(string threadId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(threadId);
        _byId.TryRemove(threadId, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryItineraryStore : IItineraryStore
{
    private readonly ConcurrentDictionary<string, List<Itinerary>> _byId = new();

    public Task<Itinerary?> GetLatestAsync(string itineraryId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(itineraryId);
        return Task.FromResult(_byId.TryGetValue(itineraryId, out var v) ? v[^1] : null);
    }

    public Task<Itinerary?> GetVersionAsync(string itineraryId, int version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(itineraryId);
        if (!_byId.TryGetValue(itineraryId, out var versions))
        {
            return Task.FromResult<Itinerary?>(null);
        }
        return Task.FromResult<Itinerary?>(versions.FirstOrDefault(x => x.Version == version));
    }

    public Task<Itinerary> AppendVersionAsync(Itinerary itinerary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(itinerary);
        var list = _byId.GetOrAdd(itinerary.Id, _ => new List<Itinerary>());
        lock (list)
        {
            var next = itinerary with { Version = list.Count + 1, CreatedUtc = DateTimeOffset.UtcNow };
            list.Add(next);
            return Task.FromResult(next);
        }
    }

    public async IAsyncEnumerable<Itinerary> ListByThreadAsync(string threadId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(threadId);
        foreach (var versions in _byId.Values)
        {
            ct.ThrowIfCancellationRequested();
            var latest = versions[^1];
            if (latest.ThreadId == threadId)
            {
                yield return latest;
            }
        }
        await Task.CompletedTask;
    }
}
