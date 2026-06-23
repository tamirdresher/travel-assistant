using TravelAssistant.Agent.Abstractions.Providers;
using Xunit;

namespace TravelAssistant.Agent.Abstractions.Tests;

/// <summary>
/// Locks the deterministic, fixture-stable behavior of the InMemory providers used when
/// TRAVEL_PROVIDERS=InMemory. APP-1 E2E + fixture recording rely on byte-stable output.
/// </summary>
public class InMemoryProvidersTests
{
    private static readonly DateOnly Date = new(2026, 6, 1);

    [Fact]
    public async Task Flight_provider_is_deterministic_across_calls()
    {
        var p = new InMemoryFlightProvider();
        var search = new FlightSearch("TLV", "LIS", Date, 2);
        var a = await p.SearchAsync(search);
        var b = await p.SearchAsync(search);
        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a[0].ProviderRefId, b[0].ProviderRefId);
        Assert.Equal(a[0].DepartsAt, b[0].DepartsAt);
        Assert.Equal(a[0].PriceUsd, b[0].PriceUsd);
    }

    [Fact]
    public async Task Flight_provider_scales_price_by_travelers()
    {
        var p = new InMemoryFlightProvider();
        var solo = (await p.SearchAsync(new FlightSearch("TLV", "LIS", Date, 1)))[0];
        var pair = (await p.SearchAsync(new FlightSearch("TLV", "LIS", Date, 2)))[0];
        Assert.Equal(solo.PriceUsd * 2, pair.PriceUsd);
    }

    [Fact]
    public async Task Flight_provider_ref_id_embeds_route_and_date()
    {
        var p = new InMemoryFlightProvider();
        var offer = (await p.SearchAsync(new FlightSearch("TLV", "LIS", Date, 1)))[0];
        Assert.Equal("inmem-TLV-LIS-20260601", offer.ProviderRefId);
        Assert.Equal("TLV", offer.Origin);
        Assert.Equal("LIS", offer.Destination);
    }

    [Fact]
    public async Task Lodging_provider_returns_one_stable_offer()
    {
        var p = new InMemoryLodgingProvider();
        var offers = await p.SearchAsync(new LodgingSearch("Lisbon", Date, Date.AddDays(2), 2));
        Assert.Single(offers);
        Assert.Equal("inmem-stay-Lisbon-20260601", offers[0].ProviderRefId);
        Assert.Equal(120m, offers[0].NightlyUsd);
    }

    [Fact]
    public async Task Activity_provider_returns_sight_and_meal()
    {
        var p = new InMemoryActivityProvider();
        var offers = await p.SearchAsync(new ActivitySearch("Lisbon", Date));
        Assert.Equal(2, offers.Count);
        Assert.Contains(offers, o => o.Kind == ActivityKind.Sight);
        Assert.Contains(offers, o => o.Kind == ActivityKind.Meal);
    }

    [Fact]
    public async Task All_providers_throw_on_null_search()
    {
        var f = new InMemoryFlightProvider();
        var l = new InMemoryLodgingProvider();
        var a = new InMemoryActivityProvider();
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.SearchAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => l.SearchAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => a.SearchAsync(null!));
    }

    [Fact]
    public void All_providers_report_InMemory_provider_id()
    {
        Assert.Equal("InMemory", new InMemoryFlightProvider().ProviderId);
        Assert.Equal("InMemory", new InMemoryLodgingProvider().ProviderId);
        Assert.Equal("InMemory", new InMemoryActivityProvider().ProviderId);
    }
}
