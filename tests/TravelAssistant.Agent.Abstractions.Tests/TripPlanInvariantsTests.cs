using TravelAssistant.Agent.Abstractions;
using Xunit;

namespace TravelAssistant.Agent.Abstractions.Tests;

/// <summary>
/// QA contract gate for APP-2. Locks the wire surface of TripPlanInvariants so any provider
/// adapter or LLM-generated plan must satisfy these rules. XD-5 grounding rules are explicitly covered.
/// </summary>
public class TripPlanInvariantsTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 3);
    private static readonly SourceRef Src = new("InMemory", "ref-1", DateTime.UtcNow, "https://example.invalid");

    private static Flight ValidFlight(string id = "f1", string from = "TLV", string to = "LIS")
        => new(id, from, to,
            new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
            "InMemory Air", "IM100", 200m, PlanItemStatus.Grounded, [Src]);

    private static Activity ValidActivity(DateOnly day, string id = "a1")
        => new(id, ActivityKind.Sight, "Walk", day.ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc),
            60, 25m, "Old Town", PlanItemStatus.Grounded, [Src]);

    private static TripPlan ValidPlan()
        => new("p1", "Lisbon", Start, End, "USD", 225m,
            [ValidFlight()],
            [new TripDay(Start, 1, [ValidActivity(Start)])],
            [Src]);

    [Fact]
    public void Valid_plan_passes()
        => Assert.Empty(TripPlanInvariants.Validate(ValidPlan()));

    [Fact]
    public void End_before_start_fails()
    {
        var p = ValidPlan() with { End = Start.AddDays(-1) };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("end is before start"));
    }

    [Theory]
    [InlineData("tlv")]   // lowercase
    [InlineData("TL")]    // too short
    [InlineData("TLVX")]  // too long
    [InlineData("TL1")]   // digit
    public void Invalid_IATA_origin_fails(string bad)
    {
        var p = ValidPlan() with { Flights = [ValidFlight() with { Origin = bad }] };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("invalid IATA origin"));
    }

    [Fact]
    public void Origin_equals_destination_fails()
    {
        var p = ValidPlan() with { Flights = [ValidFlight() with { Destination = "TLV" }] };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("origin == destination"));
    }

    [Fact]
    public void ArrivesAt_not_after_departsAt_fails()
    {
        var f = ValidFlight();
        var bad = f with { ArrivesAt = f.DepartsAt };
        var p = ValidPlan() with { Flights = [bad] };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("arrivesAt must be after departsAt"));
    }

    [Fact]
    public void XD5_Grounded_flight_without_sources_fails()
    {
        var p = ValidPlan() with { Flights = [ValidFlight() with { Sources = null }] };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("Grounded items must have at least one source"));
    }

    [Fact]
    public void XD5_Pending_flight_with_flightNumber_fails()
    {
        var p = ValidPlan() with
        {
            Flights =
            [
                ValidFlight() with { Status = PlanItemStatus.Pending, Sources = null, FlightNumber = "AA42" }
            ]
        };
        Assert.Contains(TripPlanInvariants.Validate(p),
            e => e.Contains("Pending items must not emit a specific flight number"));
    }

    [Fact]
    public void Day_outside_trip_range_fails()
    {
        var p = ValidPlan() with
        {
            Days = [new TripDay(End.AddDays(5), 1, [ValidActivity(End.AddDays(5))])]
        };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("outside trip range"));
    }

    [Fact]
    public void Duplicate_dayNumber_fails()
    {
        var p = ValidPlan() with
        {
            Days =
            [
                new TripDay(Start, 1, [ValidActivity(Start, "a1")]),
                new TripDay(Start.AddDays(1), 1, [ValidActivity(Start.AddDays(1), "a2")])
            ]
        };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("duplicate dayNumber"));
    }

    [Fact]
    public void Activity_on_wrong_day_fails()
    {
        var p = ValidPlan() with
        {
            Days = [new TripDay(Start, 1, [ValidActivity(Start.AddDays(2))])]
        };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("not on day"));
    }

    [Fact]
    public void TotalCost_mismatch_fails()
    {
        var p = ValidPlan() with { TotalCost = 999m };
        Assert.Contains(TripPlanInvariants.Validate(p), e => e.Contains("totalCost"));
    }

    [Fact]
    public void TotalCost_rounding_within_one_cent_passes()
    {
        // 200 (flight) + 25.005 (activity) -> sum 225.005, totalCost 225.00 -> diff 0.005 <= 0.01
        var p = ValidPlan() with
        {
            Days = [new TripDay(Start, 1, [ValidActivity(Start) with { CostUsd = 25.005m }])],
            TotalCost = 225.00m
        };
        Assert.Empty(TripPlanInvariants.Validate(p));
    }

    [Fact]
    public void EnsureValid_throws_on_invalid()
    {
        var p = ValidPlan() with { End = Start.AddDays(-1) };
        Assert.Throws<InvalidPlanException>(() => TripPlanInvariants.EnsureValid(p));
    }

    [Fact]
    public void EnsureValid_does_not_throw_on_valid()
        => TripPlanInvariants.EnsureValid(ValidPlan());
}
