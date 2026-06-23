using System.Text.RegularExpressions;

namespace TravelAssistant.Agent.Abstractions;

/// <summary>
/// Contract invariants enforced over a <see cref="TripPlan"/>. APP-2 / QA contract gate.
/// Implementations and provider adapters MUST produce plans that pass <see cref="Validate"/>.
/// </summary>
public static partial class TripPlanInvariants
{
    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex IataRegex();

    public static IReadOnlyList<string> Validate(TripPlan plan)
    {
        var errors = new List<string>();
        if (plan is null) { errors.Add("plan is null"); return errors; }
        if (plan.End < plan.Start) errors.Add("end is before start");

        foreach (var f in plan.Flights)
        {
            if (!IataRegex().IsMatch(f.Origin)) errors.Add($"flight {f.Id}: invalid IATA origin '{f.Origin}'");
            if (!IataRegex().IsMatch(f.Destination)) errors.Add($"flight {f.Id}: invalid IATA destination '{f.Destination}'");
            if (f.Origin == f.Destination) errors.Add($"flight {f.Id}: origin == destination");
            if (f.ArrivesAt <= f.DepartsAt) errors.Add($"flight {f.Id}: arrivesAt must be after departsAt");
            if (f.Status == PlanItemStatus.Grounded && (f.Sources is null || f.Sources.Count == 0))
                errors.Add($"flight {f.Id}: Grounded items must have at least one source (XD-5 rule)");
            if (f.Status == PlanItemStatus.Pending && f.FlightNumber is not null)
                errors.Add($"flight {f.Id}: Pending items must not emit a specific flight number (XD-5 rule)");
        }

        var seenDays = new HashSet<int>();
        foreach (var d in plan.Days)
        {
            if (d.Date < plan.Start || d.Date > plan.End)
                errors.Add($"day {d.DayNumber}: date {d.Date:O} outside trip range {plan.Start:O}..{plan.End:O}");
            if (!seenDays.Add(d.DayNumber)) errors.Add($"duplicate dayNumber {d.DayNumber}");

            foreach (var a in d.Activities)
            {
                var actDate = DateOnly.FromDateTime(a.StartsAt);
                if (actDate != d.Date)
                    errors.Add($"activity {a.Id}: startsAt {a.StartsAt:O} not on day {d.Date:O}");
                if (a.DurationMinutes <= 0) errors.Add($"activity {a.Id}: duration must be > 0");
                if (a.Status == PlanItemStatus.Grounded && (a.Sources is null || a.Sources.Count == 0))
                    errors.Add($"activity {a.Id}: Grounded items must have at least one source");
            }
        }

        var sum = plan.Flights.Sum(f => f.PriceUsd ?? 0m)
                + plan.Days.SelectMany(d => d.Activities).Sum(a => a.CostUsd ?? 0m);
        if (Math.Abs(sum - plan.TotalCost) > 0.01m)
            errors.Add($"totalCost {plan.TotalCost} does not equal sum of line items {sum}");

        return errors;
    }

    public static void EnsureValid(TripPlan plan)
    {
        var errors = Validate(plan);
        if (errors.Count > 0)
            throw new InvalidPlanException(string.Join("; ", errors));
    }
}

public sealed class InvalidPlanException(string message) : Exception(message);
