namespace TravelAssistant.Api.Telemetry;

/// <summary>
/// Canonical custom OTel metric names. These names are a contract with
/// azure-infrastructure-squad's dashboards and alert rules
/// (see docs/architecture/observability-metrics.md, APP-10).
///
/// Do not rename or change case without an ADR and a coordinated PR
/// across app-dev and azure-infra.
/// </summary>
public static class MetricNames
{
    /// <summary>The single Meter name shared by all custom Travel Assistant metrics.</summary>
    public const string MeterName = "TravelAssistant.Agent";

    /// <summary>Counter&lt;long&gt; — prompt tokens sent to the LLM. Tags: model, operation.</summary>
    public const string LlmTokensIn = "llm.tokens.in";

    /// <summary>Counter&lt;long&gt; — completion tokens received from the LLM. Tags: model, operation.</summary>
    public const string LlmTokensOut = "llm.tokens.out";

    /// <summary>Counter&lt;double&gt; — USD cost for the call (tokens × per-model rate). Tags: model, operation.</summary>
    public const string LlmCostUsd = "llm.cost.usd";

    /// <summary>Counter&lt;long&gt; — chip cache lookup. Tags: chip_kind, result ("hit"|"miss").</summary>
    public const string ChipCacheHit = "chip.cache.hit";
}
