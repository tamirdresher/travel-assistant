using Serilog;
using TravelAssistant.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

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

// Health check endpoint
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

app.Run();

// Request/Response DTOs
record SearchRequest(string[]? Destinations, DateOnly? DepartureDate, DateOnly? ReturnDate, int? PassengerCount);

record SearchResponse
{
    public required string Message { get; init; }
    public required string RequestId { get; init; }
    public required DateTime Timestamp { get; init; }
}

// Expose Program for WebApplicationFactory<Program> in test assemblies.
public partial class Program { }
