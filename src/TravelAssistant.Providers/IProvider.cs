namespace TravelAssistant.Providers;

public interface IProvider<in TQuery, TResult>
{
    Task<IReadOnlyList<TResult>> SearchAsync(TQuery query, CancellationToken ct = default);
}

public sealed record FlightQuery(string OriginIata, string DestinationIata, DateOnly Departure, DateOnly? Return, int Passengers);
public sealed record FlightResult(string Carrier, string FlightNumber, DateTimeOffset DepartUtc, DateTimeOffset ArriveUtc, decimal PriceUsd);

public sealed record HotelQuery(string CityIata, DateOnly CheckIn, DateOnly CheckOut, int Guests);
public sealed record HotelResult(string Name, string Address, int StarRating, decimal NightlyUsd);

public sealed record PlaceQuery(string CityIata, string Category);
public sealed record PlaceResult(string Name, string Category, double Lat, double Lon);

public interface IFlightProvider : IProvider<FlightQuery, FlightResult> { }
public interface IHotelProvider : IProvider<HotelQuery, HotelResult> { }
public interface IPlaceProvider : IProvider<PlaceQuery, PlaceResult> { }
