using Serilog;
using TravelAssistant.Agent;
using TravelAssistant.Agent.Abstractions;
using TravelAssistant.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

// APP-1 — Aspire service defaults (OTel, health, resilient HTTP, discovery).
builder.AddServiceDefaults();

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
});

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// LOGIN-001 — auth services. Two-layer RL is IMemoryCache-backed in dev;
// per §5 the prod swap MUST be a distributed cache.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();
builder.Services.AddSingleton<ILoginAuditLog, LoginAuditLog>();
builder.Services.AddSingleton<IAccessTokenIssuer, RsaAccessTokenIssuer>();
builder.Services.AddSingleton<IUserLookup>(_ => new InMemoryUserLookup(Array.Empty<UserRecord>()));
// LOGIN-002 — RFC 7239 Forwarded-aware client IP resolver. Reads
// `Auth:TrustedProxyCidrs` from configuration; empty list (default) means
// peer IP is always used (correct for single-node dev).
builder.Services.AddSingleton<IClientIpResolver, RfcForwardedClientIpResolver>();

// APP-3 — ITravelAgent stub (Semantic Kernel impl coming in APP-3 follow-up).
builder.Services.AddSingleton<ITravelAgent, StubTravelAgent>();

var app = builder.Build();

// APP-1 — Aspire default endpoints (/health, /alive in Development).
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

// LOGIN-001 — POST /api/auth/login (activates login-gate.yml code-presence checks).
app.MapLoginEndpoint();

// Health check endpoint — preserved for login-gate.yml workflow compatibility.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .Produces<object>(StatusCodes.Status200OK);

// Placeholder search endpoint
app.MapPost("/api/search", (SearchRequest request) =>
{
    app.Logger.LogInformation("Search request received for destinations: {Destinations}", 
        string.Join(", ", request.Destinations ?? Array.Empty<string>()));
    
    return Results.Ok(new SearchResponse
    {
        Message = "Search endpoint placeholder - implementation pending",
        RequestId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow
    });
})
.WithName("SearchFlightsAndHotels")
.WithTags("Search")
.Produces<SearchResponse>(StatusCodes.Status200OK);

// APP-1 + APP-3 — minimal E2E itinerary planning endpoint.
app.MapPost("/api/itinerary/plan", async (PlanTripRequest req, ITravelAgent agent, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Destination))
        return Results.BadRequest(new { error = "destination is required" });
    if (req.Days < 1 || req.Days > 30)
        return Results.BadRequest(new { error = "days must be between 1 and 30" });

    var plan = await agent.PlanTripAsync(
        new TripRequest(req.Destination, req.Days, req.BudgetUsd, req.Interests), ct);
    return Results.Ok(plan);
})
.WithName("PlanItinerary")
.WithTags("Itinerary")
.Produces<TripPlan>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.Run();

// Request/Response DTOs
record SearchRequest(string[]? Destinations, DateOnly? DepartureDate, DateOnly? ReturnDate, int? PassengerCount);

record SearchResponse
{
    public required string Message { get; init; }
    public required string RequestId { get; init; }
    public required DateTime Timestamp { get; init; }
}

record PlanTripRequest(string Destination, int Days, decimal? BudgetUsd, IReadOnlyList<string>? Interests);

// Expose Program for WebApplicationFactory<Program> in test assemblies.
public partial class Program { }
