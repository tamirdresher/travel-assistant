# Custom OTel Metrics Contract (APP-10)

**Status:** Accepted · 2026-06-23
**Owners:** application-development-squad
**Consumers:** azure-infrastructure-squad (INF-5 dashboards + alerts)

## Confirmation

The following custom metric names are **confirmed as-is** for INF-5 dashboard JSON and alert KQL. Azure-infra may wire dashboards and alerts against these literal names with no changes.

| Metric name | Instrument | Unit | Tags | Owner |
|---|---|---|---|---|
| `llm.tokens.in` | Counter\<long> | `{token}` | `model`, `operation` | Agent (APP-3/APP-4) |
| `llm.tokens.out` | Counter\<long> | `{token}` | `model`, `operation` | Agent |
| `llm.cost.usd` | Counter\<double> | `USD` | `model`, `operation` | Agent (cost meter, APP-4 guardrail) |
| `chip.cache.hit` | Counter\<long> | `{hit}` | `chip_kind`, `result` (`hit`/`miss`) | Api / Agent |

**Meter name:** `TravelAssistant.Agent` (single `Meter` instance per process; `chip.cache.hit` also emitted under same meter to keep dashboard queries simple).

**Names match the contract exactly — same case, same dots, same plurality.** Codified in `src/TravelAssistant.Api/Telemetry/MetricNames.cs` so any future emitter that uses the constants cannot drift.

## Emission Sites

| Metric | Emitted where | Tied to backlog item |
|---|---|---|
| `llm.tokens.in` / `llm.tokens.out` | `ITravelAgent` adapter after each model call (response.Usage) | APP-3 |
| `llm.cost.usd` | Same adapter; computed as `tokens × per-model rate` from `appsettings:Llm:Pricing` | APP-4 |
| `chip.cache.hit` | Chip cache layer when a chip rendering is served from cache vs newly generated | APP-2 hub |

## Required OTel Configuration

ServiceDefaults must include the `TravelAssistant.Agent` meter in `MeterProviderBuilder.AddMeter("TravelAssistant.Agent")`. Existing `AddBuiltInMeters()` already adds AspNetCore + HttpClient + Runtime — Agent meter is the only addition.

```csharp
metrics.AddMeter("TravelAssistant.Agent");
```

The Azure Monitor exporter (configured in ServiceDefaults) propagates these to App Insights as `customMetrics` with the names above.

## Alert Wiring (informative, owned by azure-infra)

| Alert | Query against | Threshold (initial) |
|---|---|---|
| Token spend spike | sum(`llm.tokens.in` + `llm.tokens.out`) over 5m | > 500k tokens/5m |
| Cost guardrail breach | sum(`llm.cost.usd`) over 1h | > $20/h |
| Chip cache collapse | (count(`chip.cache.hit` where result=hit) / count(`chip.cache.hit`)) over 15m | < 0.30 |

These thresholds are azure-infra's call; metric names + tag keys are the contract.

## Changes

Renaming any metric requires an ADR + coordinated PR across app-dev (emitter) and azure-infra (dashboard JSON + alert KQL). Do not silently rename.
