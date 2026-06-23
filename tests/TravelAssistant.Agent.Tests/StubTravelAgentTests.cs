using TravelAssistant.Agent;
using TravelAssistant.Agent.Abstractions;
using Xunit;

namespace TravelAssistant.Agent.Tests;

public class StubTravelAgentTests
{
    [Fact]
    public async Task PlanTripAsync_ReturnsRequestedDays_WithLisbonInTitles()
    {
        var sut = new StubTravelAgent();
        var plan = await sut.PlanTripAsync(new TripRequest("Lisbon", 3));
        Assert.Equal("Lisbon", plan.Destination);
        Assert.Equal(3, plan.Days);
        Assert.Equal(3, plan.Itinerary.Count);
        Assert.All(plan.Itinerary, d => Assert.Contains("Lisbon", d.Title));
        Assert.All(plan.Itinerary, d => Assert.Equal(4, d.Activities.Count));
    }

    [Fact]
    public async Task StreamPlanAsync_StreamsAtLeastOneTokenPerDay()
    {
        var sut = new StubTravelAgent();
        var chunks = new List<string>();
        await foreach (var c in sut.StreamPlanAsync(new TripRequest("Lisbon", 2)))
            chunks.Add(c);
        Assert.Contains(chunks, c => c.Contains("Day 1"));
        Assert.Contains(chunks, c => c.Contains("Day 2"));
    }

    [Fact]
    public async Task RefineTripAsync_AppendsInstructionToSummary()
    {
        var sut = new StubTravelAgent();
        var plan = await sut.PlanTripAsync(new TripRequest("Lisbon", 2));
        var refined = await sut.RefineTripAsync(plan, "more walkable");
        Assert.Contains("more walkable", refined.Summary);
    }
}
