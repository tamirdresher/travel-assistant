using TravelAssistant.Api.Security;

namespace TravelAssistant.Api;

/// <summary>
/// Wire-in glue requested by security-hardening-squad (PR #39):
///   • SEC-3 — SsrfGuardingHttpHandler attached to outbound named
///     HttpClients ("flights", "hotels", "activities", "maps", "currency").
///   • SEC-5 — /health/prod-guard endpoint + startup hard gate.
///
/// APP-6 envelope encryption is deferred to a follow-up PR — needs a
/// consumer in the data layer first.
/// </summary>
internal static class SecurityRegistration
{
    private const string SsrfAllowlistKey = "Security:Ssrf:Allowlist";
    private const string SsrfLocalhostKey = "Security:Ssrf:IsLocalhostAllowed";

    private static readonly string[] OutboundHttpClients =
    {
        "flights",
        "hotels",
        "activities",
        "maps",
        "currency",
    };

    public static WebApplicationBuilder AddSecurityWireIns(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // SEC-3 — SSRF-guarding handler on every named outbound HttpClient.
        builder.Services.AddTransient<SsrfGuardingHttpHandler>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<SsrfGuardingHttpHandler>>();
            var allowlist = cfg.GetSection(SsrfAllowlistKey).Get<string[]>() ?? Array.Empty<string>();
            var localhostOk = cfg.GetValue<bool?>(SsrfLocalhostKey) ?? false;
            return new SsrfGuardingHttpHandler(allowlist, localhostOk, logger);
        });

        foreach (var name in OutboundHttpClients)
        {
            builder.Services.AddHttpClient(name)
                .AddHttpMessageHandler<SsrfGuardingHttpHandler>();
        }

        // SEC-5 — ProductionGuard (used by startup gate + /health/prod-guard).
        builder.Services.AddSingleton<ProductionGuard>();

        return builder;
    }

    public static WebApplication UseSecurityWireIns(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // SEC-5 — Hard startup gate. Refuse to serve outside Development if
        // a developer convenience is still wired up.
        if (ProductionGuard.IsRequired(app.Environment))
        {
            var report = app.Services.GetRequiredService<ProductionGuard>().Run();
            if (!report.Ok)
            {
                throw new ProductionGuardException(report.Checks.Where(c => !c.Passed));
            }
        }

        // SEC-5 — Diagnostic endpoint for INF-4 deploy gate.
        app.MapGet("/health/prod-guard", (ProductionGuard g) =>
        {
            var r = g.Run();
            return r.Ok ? Results.Ok(r) : Results.Json(r, statusCode: 503);
        })
        .WithName("ProductionGuardHealth")
        .WithTags("Health");

        return app;
    }
}
