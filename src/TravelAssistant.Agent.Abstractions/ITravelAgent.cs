namespace TravelAssistant.Agent.Abstractions;

public interface ITravelAgent
{
    Task<TripPlan> PlanTripAsync(TripRequest request, CancellationToken ct = default);
    Task<TripPlan> RefineTripAsync(TripPlan current, string instruction, CancellationToken ct = default);
    Task<string> ExplainChoiceAsync(TripPlan plan, string choiceId, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamPlanAsync(TripRequest request, CancellationToken ct = default);
}

public record TripRequest(string Destination, int Days, decimal? BudgetUsd = null, IReadOnlyList<string>? Interests = null);

public record TripPlan(string Id, string Destination, int Days, string Summary, IReadOnlyList<TripDay> Itinerary);

public record TripDay(int DayNumber, string Title, IReadOnlyList<TripActivity> Activities);

public record TripActivity(string Id, string TimeOfDay, string Title, string? Notes = null);
