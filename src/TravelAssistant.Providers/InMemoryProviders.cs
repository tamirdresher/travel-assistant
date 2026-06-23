namespace TravelAssistant.Providers;

public sealed class InMemoryFlightProvider : IFlightProvider
{
    public Task<IReadOnlyList<FlightResult>> SearchAsync(FlightQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var depart = query.Departure.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        IReadOnlyList<FlightResult> results = new[]
        {
            new FlightResult("TA", "100", depart, depart.AddHours(3), 199.99m),
            new FlightResult("TA", "200", depart.AddHours(6), depart.AddHours(9), 249.99m),
        };
        return Task.FromResult(results);
    }
}

public sealed class InMemoryHotelProvider : IHotelProvider
{
    public Task<IReadOnlyList<HotelResult>> SearchAsync(HotelQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<HotelResult> results = new[]
        {
            new HotelResult("Fake Plaza", $"1 Main St, {query.CityIata}", 4, 159.00m),
            new HotelResult("Stub Inn", $"22 Second Ave, {query.CityIata}", 3, 89.00m),
        };
        return Task.FromResult(results);
    }
}

public sealed class InMemoryPlaceProvider : IPlaceProvider
{
    public Task<IReadOnlyList<PlaceResult>> SearchAsync(PlaceQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<PlaceResult> results = new[]
        {
            new PlaceResult($"{query.CityIata} Museum", query.Category, 38.7, -9.1),
            new PlaceResult($"{query.CityIata} Park", query.Category, 38.71, -9.11),
        };
        return Task.FromResult(results);
    }
}
