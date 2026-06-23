# 0001 — Aspire AppHost as the orchestration entry point (APP-1)

**Status:** Accepted
**Date:** 2026-06-23
**Owner:** application-development-squad (Ripley)

## Context

The Travel Assistant needs a single command to bring up the whole dev stack
(web client, API, agent gateway, Cosmos, Postgres) so any developer can go
from `git clone` to a working "plan a 3-day trip to Lisbon" flow.

## Decision

Use .NET Aspire 9.4 AppHost as the dev orchestration entry point.

- `src/TravelAssistant.AppHost/` is the only project a dev needs to run.
- Cosmos via `RunAsEmulator()` for offline-friendly dev.
- Postgres as a containerized resource via `AddPostgres(...)`.
- API gets `WithReference(...)` for both data resources, with `WaitFor`.
- All services consume `TravelAssistant.ServiceDefaults` for OTel,
  health checks, resilient HTTP, and service discovery.

## Consequences

- Single command: `dotnet run --project src/TravelAssistant.AppHost`.
- Aspire dashboard provides traces/logs/metrics out of the box.
- Container runtime (Docker Desktop / Podman) is now a dev prerequisite.
- AppHost is not deployed; production uses Bicep (infra squad owns).

## Follow-ups

- APP-3: Swap StubTravelAgent for a Semantic-Kernel-backed impl.
- APP-4: Add provider resources (flights/hotels/maps) as Aspire refs.
- APP-7: Token-budget + circuit breaker on LLM gateway.
