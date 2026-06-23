using TravelAssistant.Data;
using TravelAssistant.Providers;
using Xunit;

namespace TravelAssistant.Data.Tests;

public class ChatThreadStoreTests
{
    [Fact]
    public async Task Upsert_increments_version()
    {
        IChatThreadStore store = new InMemoryChatThreadStore();
        var t = new ChatThread("t1", "u1", "Lisbon trip", 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<ChatMessage>());

        var v1 = await store.UpsertAsync(t);
        var v2 = await store.UpsertAsync(t with { Title = "Lisbon 3-day" });

        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
        var latest = await store.GetAsync("t1");
        Assert.Equal("Lisbon 3-day", latest!.Title);
        var first = await store.GetAsync("t1", version: 1);
        Assert.Equal("Lisbon trip", first!.Title);
    }

    [Fact]
    public async Task List_by_user_returns_latest_only()
    {
        IChatThreadStore store = new InMemoryChatThreadStore();
        await store.UpsertAsync(new ChatThread("t1", "u1", "a", 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<ChatMessage>()));
        await store.UpsertAsync(new ChatThread("t1", "u1", "b", 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<ChatMessage>()));
        await store.UpsertAsync(new ChatThread("t2", "u2", "c", 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<ChatMessage>()));

        var list = new List<ChatThread>();
        await foreach (var x in store.ListByUserAsync("u1")) list.Add(x);

        Assert.Single(list);
        Assert.Equal("b", list[0].Title);
    }
}

public class ProviderContractTests
{
    [Fact]
    public async Task Flight_provider_returns_results_for_iata_query()
    {
        IFlightProvider p = new InMemoryFlightProvider();
        var results = await p.SearchAsync(new FlightQuery("LIS", "MAD", new DateOnly(2026, 7, 1), null, 1));
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.PriceUsd > 0));
    }

    [Fact]
    public async Task Hotel_provider_echoes_city_iata_in_address()
    {
        IHotelProvider p = new InMemoryHotelProvider();
        var results = await p.SearchAsync(new HotelQuery("LIS", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 4), 2));
        Assert.All(results, r => Assert.Contains("LIS", r.Address, StringComparison.Ordinal));
    }
}
