# ADR 0001: Web Framework — Blazor (Server + WebAssembly hybrid)

- **Status:** Accepted
- **Date:** 2026-06-23
- **Owners:** application-development-squad (Ripley, Newt)
- **Issue:** APP-6 (Web client shell)
- **Deciders:** application-development-squad; consulted: experience-design-squad, security-hardening-squad

## Context

APP-6 requires an auth-gated chat shell with streaming token rendering (from APP-2 SignalR hub),
thread list, itinerary panel, and shadcn-equivalent components (XD-3). The decision is between
**Next.js (React/TypeScript)** and **Blazor (.NET, Server + WASM hybrid)**.

Constraints:

- AppHost orchestrates the full stack via .NET Aspire (`TravelAssistant.AppHost`).
- Backend is .NET 9 minimal API + SignalR hub + Semantic Kernel.
- Security squad enforces SEC-2 (prompt-injection sanitization) and SEC-3 (SSRF guard) on the
  server boundary — all model interaction stays server-side.
- Token streaming uses SignalR; `Microsoft.AspNetCore.SignalR.Client` is first-class in both stacks.
- Target Lighthouse a11y ≥ 95; WCAG 2.2 AA (XD-3 / Pris).

## Decision

**Adopt Blazor with an interactive render-mode hybrid:**

- **Blazor Server** for the chat shell (live SignalR streaming, low first-paint, no model state on the client).
- **Blazor WebAssembly** for the itinerary panel and offline-friendly read views.
- **InteractiveAuto** render mode so Server boots first and WASM takes over silently when cached.

Web project: `apps/TravelAssistant.Web` (Blazor Web App template, .NET 9).
Component library: `apps/TravelAssistant.Web.Components` (shared Razor class library).

## Consequences

### Positive

- **Single language / single runtime.** No TS↔C# DTO drift; reuse `TravelAssistant.Contracts` (APP-2 TripPlan models) directly in the UI.
- **Aspire-native.** First-class `AddProject<TravelAssistant_Web>()`; built-in service discovery + OTel propagation.
- **SignalR streaming is trivial.** Same `HubConnection` client on both render modes.
- **Auth alignment.** Server-side cookie auth + antiforgery composes with the API stack; no separate NextAuth/JWT bridge.
- **No Node toolchain in CI.** Reduces supply-chain surface (SEC-1 / SEC-4 wins); one `dotnet publish` produces the deploy artifact.
- **Tailwind + shadcn-equivalent.** Tailwind v4 + a Razor port of Radix/shadcn primitives satisfies XD-3 tokens without a JSX runtime.

### Negative / accepted trade-offs

- Smaller ecosystem of pre-built AI-chat UI kits than React/Next.js. Acceptable — chat shell is not complex enough to justify the framework swap.
- WASM payload cost on first load — mitigated by InteractiveAuto + AOT in Release.
- Smaller talent pool than React. Documented in onboarding; Newt's charter updated.

### Rejected: Next.js

- Forces a second language + build system + DTO-sync layer that duplicates `TravelAssistant.Contracts`.
- Adds Node to the supply chain — every npm transitive is a new SEC-1 surface.
- SignalR-from-TS works but is second-class.
- Aspire `AddNpmApp` is strictly weaker than `AddProject`.

## Acceptance criteria (APP-6)

- [ ] `apps/TravelAssistant.Web` boots under AppHost.
- [ ] Chat page renders streaming tokens from APP-2 hub.
- [ ] Itinerary panel hydrates from APP-5 Cosmos-backed thread store.
- [ ] Auth-gated routes redirect unauthenticated users.
- [ ] Lighthouse a11y ≥ 95 on chat + itinerary pages.
- [ ] All Tailwind tokens sourced from XD-3 token export (no inline color literals).

## References

- APP-6 backlog item (ideation-research-planning-squad DM, 2026-06-23).
- XD-3 design system / token export (experience-design-squad).
- SEC-2 prompt-injection sanitizer contract (security-hardening-squad).
- .NET Aspire Blazor integration docs.
