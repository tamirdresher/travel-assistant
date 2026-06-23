# Travel Assistant API Contracts (APP-2 / APP-4)

**Status:** Stable. QA records fixtures against this surface. Breaking changes require a coordinated PR with `quality-testing-squad`.

## ITravelAgent (APP-2)

Interface in `TravelAssistant.Agent.Abstractions.ITravelAgent`. Four operations:

| Method | Returns | Wire shape |
|---|---|---|
| `PlanTripAsync(TripRequest)` | `TripPlan` | Validates against `Schemas/itinerary.schema.json` |
| `RefineTripAsync(TripPlan, string)` | `TripPlan` | Same schema; ID may change |
| `ExplainChoiceAsync(TripPlan, activityId)` | `ChoiceExplanation` | Rationale + grounded `sources[]` |
| `StreamPlanAsync(TripRequest)` | `IAsyncEnumerable<TripPlanDelta>` | JSON-Patch-ish deltas |

### Invariants (enforced by `TripPlanInvariants.Validate`)

1. `end >= start`.
2. `flight.origin` / `flight.destination` are valid 3-letter uppercase IATA codes, and not equal.
3. `flight.arrivesAt > flight.departsAt`.
4. Every `day.date` is within `[plan.start, plan.end]`; `dayNumber` is unique per plan.
5. Every `activity.startsAt`'s date equals its enclosing `day.date`.
6. `activity.durationMinutes > 0`.
7. `totalCost == sum(flights.priceUsd) + sum(activities.costUsd)` (±0.01 USD).
8. **Grounding (XD-5):** `status: Grounded` items MUST include at least one `SourceRef`.
9. **No ungrounded specifics (XD-5):** `status: Pending` flights MUST NOT include `flightNumber`.

## Provider adapters (APP-4)

Interfaces: `IFlightProvider`, `ILodgingProvider`, `IActivityProvider`. Each has a stable `ProviderId` and returns provider-agnostic offer DTOs. Adapters MUST:

- Retry idempotently on 5xx (use `Microsoft.Extensions.Http.Resilience`).
- Throw `ProviderUnavailableException` on terminal failure.
- Never leak raw provider JSON to callers.
- Be recordable at the HTTP layer (use `HttpClientFactory` so QA can attach a `DelegatingHandler`).

## In-memory profile (APP-1)

Set `TRAVEL_PROVIDERS=InMemory` to register `InMemoryFlightProvider`, `InMemoryLodgingProvider`, `InMemoryActivityProvider` in DI. Output is deterministic — fixtures don't drift across runs. This is the profile QA uses for E2E in the `DistributedApplicationTestingBuilder` AppHost.

## Web client selectors (APP-3, for `apps/web`)

QA asked for stable `data-testid` hooks. The contract:

| testid | Element |
|---|---|
| `itinerary-root` | Top-level itinerary canvas container |
| `itinerary-total-cost` | The total cost figure |
| `itinerary-day-{n}` | Day card, `n` = `dayNumber` (1-based) |
| `itinerary-activity-{id}` | One activity row |
| `itinerary-flight-{id}` | One flight row |
| `chat-input` | Chat textarea |
| `chat-send` | Send button |
| `refinement-chip-{label}` | Each refinement chip (label = kebab-case) |
| `provenance-source-{refId}` | Source link rendered next to grounded content |
| `pending-badge` | The "pending — needs retrieval" badge from XD-5 |

Selectors are part of the public contract. Renaming or removing one requires a coordinated PR.

## Schema location

`src/TravelAssistant.Agent.Abstractions/Schemas/itinerary.schema.json` — packed into the NuGet under `schemas/` so other repos can consume it.
