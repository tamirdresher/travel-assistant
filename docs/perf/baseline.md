# Performance Baseline (QA-5)

Owner: quality-testing-squad (Frost).
Status: **scaffolded — no real numbers yet**. App is pre-implementation; this file
holds the agreed *targets* + measurement protocol so we don't argue about them later.

## SUT topology under load

- AppHost runs in `InMemory` provider profile (no real flight/hotel APIs).
- LLM swapped for `StubLlm` returning a 12-chunk SSE stream at a fixed cadence.
- Chat hub: SignalR over `/hubs/chat`.

## Targets

| Metric | Target | Why |
|---|---|---|
| Concurrent threads | 100 | Free-tier demo realistic upper bound. |
| p95 first-token latency | < 1500 ms | Perceived "instant" reply with stub LLM. |
| p99 first-token latency | < 3000 ms | Tail must not collapse the UX. |
| Steady-state errors | < 0.1% | Anything higher means infra needs INF-5 alert. |
| Soak duration | 30 min | Catches socket / memory leaks. |

## Tool

k6 (preferred — better SignalR ergonomics via `xk6-signalr` build). NBomber is the
fallback if dotnet-native is required for the CI runner.

## Where the script lives

`tests/perf/chat-stream.js` — committed in a follow-up PR once APP-1 exposes the hub.

## Alert thresholds

These get wired into INF-5 Application Insights alerts:

- p95 first-token > 2000 ms for 5 min → page on-call.
- Error rate > 1% for 2 min → page on-call.
- Concurrent connections > 250 → scale-out trigger (not a page).

## Baseline numbers

| Date | p50 | p95 | p99 | Errors | Commit |
|---|---|---|---|---|---|
| _pending APP-1_ | — | — | — | — | — |
