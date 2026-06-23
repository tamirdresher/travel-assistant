using System.Runtime.CompilerServices;
using TravelAssistant.Agent.Abstractions;

namespace TravelAssistant.Agent;

public sealed class StubTravelAgent : ITravelAgent
{
    public Task<TripPlan> PlanTripAsync(TripRequest request, CancellationToken ct = default)
    {
        var days = new List<TripDay>();
        for (var i = 1; i <= request.Days; i++)
        {
            var acts = new List<TripActivity>
            {
                new($"d{i}-morning",   "Morning",   $"Explore {request.Destination} — neighborhood walk"),
                new($"d{i}-midday",    "Midday",    $"Lunch at a local spot in {request.Destination}"),
                new($"d{i}-afternoon", "Afternoon", $"Visit a landmark in {request.Destination}"),
                new($"d{i}-evening",   "Evening",   $"Dinner & sunset in {request.Destination}")
            };
            days.Add(new TripDay(i, $"Day {i} in {request.Destination}", acts));
        }
        var plan = new TripPlan(
            Id: Guid.NewGuid().ToString("N"),
            Destination: request.Destination,
            Days: request.Days,
            Summary: $"A {request.Days}-day plan for {request.Destination} (stub).",
            Itinerary: days);
        return Task.FromResult(plan);
    }

    public Task<TripPlan> RefineTripAsync(TripPlan current, string instruction, CancellationToken ct = default)
        => Task.FromResult(current with { Summary = $"{current.Summary} Refinement: {instruction}." });

    public Task<string> ExplainChoiceAsync(TripPlan plan, string choiceId, CancellationToken ct = default)
        => Task.FromResult($"Choice {choiceId} was selected to balance pacing across the {plan.Days}-day trip.");

    public async IAsyncEnumerable<string> StreamPlanAsync(TripRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var plan = await PlanTripAsync(request, ct);
        foreach (var day in plan.Itinerary)
        {
            yield return $"{day.Title}\n";
            foreach (var a in day.Activities)
            {
                ct.ThrowIfCancellationRequested();
                yield return $"  - {a.TimeOfDay}: {a.Title}\n";
                await Task.Delay(5, ct);
            }
        }
    }
}
