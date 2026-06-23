# SEC-5 — Aspire Dev-Loop Hardening for Production

**Owner:** Vasquez · **Coordinates with:** azure-infrastructure-squad (INF-4)

## What "dev loop" leaves on by default

Aspire's local AppHost wires up developer conveniences that are great
for `dotnet run` and *catastrophic* in production:

| Switch | Default in `Development` | Must be in non-`Development` |
|--------|--------------------------|------------------------------|
| OpenAPI / Swagger UI | enabled | **disabled** |
| CORS `AllowAnyOrigin` | enabled | **deny + per-env allowlist** |
| Cosmos DB emulator endpoint | wired | **must not be configured** |
| Azure Storage emulator (Azurite) | wired | **must not be configured** |
| Aspire dashboard | exposed | **internal only / disabled** |
| Detailed errors / developer exception page | on | **off** |
| HTTP (not HTTPS) endpoints | accepted | **rejected by `UseHttpsRedirection` + HSTS** |
| `SsrfGuardingHttpHandler.IsLocalhostAllowed` (SEC-3) | true | **false** |

## Controls

### C1 — Environment-keyed CORS
- `Development`: `AllowAnyOrigin` permitted on `/api/*` only.
- `Staging` / `Production`: explicit per-env origin list from configuration `Cors:AllowedOrigins`. Empty list = no CORS.

### C2 — Emulator detection
On startup, `ProductionGuard` inspects the resolved configuration for
emulator signatures:
- Cosmos: `AccountEndpoint=https://localhost:8081`
- Azurite: `UseDevelopmentStorage=true` or `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1`
- Any `localhost` / `127.0.0.1` / `host.docker.internal` outside `Development`

If detected in any non-`Development` environment, the app **fails to
start**. No flag to override; the only way past is to remove the
emulator entry.

### C3 — `/health/prod-guard` endpoint
A health endpoint exposed only on the management port. Returns 200 with
the list of checks performed, or 503 if any check failed. Designed for
the deploy gate in INF-4: `azd up` runs and the deploy job calls this
endpoint before flipping the Container App revision to active.

Checks performed:
- `appsettings*.json` contains no secret-like keys (SEC-1)
- No emulator endpoints (C2)
- CORS is not `AllowAnyOrigin` (C1)
- Developer exception page is OFF
- HTTPS redirection is ON, HSTS is enabled
- `SsrfGuardingHttpHandler.IsLocalhostAllowed` is `false` (SEC-3)
- `KeyVault:Uri` is present and resolves via managed identity (SEC-1)

### C4 — Build-time fail
`ProductionGuard` runs in two places:
1. **At app startup** — throws and refuses to serve traffic. This is
   the hard gate.
2. **In `/health/prod-guard`** — returns 503 with details. This is the
   diagnostic surface for CI / azd.

The build fails if `ProductionGuard.IsRequired(env)` is true and any
check returns failure — surfaced via the integration test
`ProductionGuardTests.NonDevelopmentEnvironment_FailsOnDevSwitches`.

## Acceptance criteria (from SEC-5)
- [x] Emulators disabled in non-dev (C2 + ProductionGuard)
- [x] CORS locked down per environment (C1)
- [x] Production health-check that fails build if any dev-only switch is on (C3 + C4)
- [x] Coordinate with azure-infrastructure-squad on INF-4 (this doc names the contract — `/health/prod-guard` returning 200 is the deploy-gate signal)
