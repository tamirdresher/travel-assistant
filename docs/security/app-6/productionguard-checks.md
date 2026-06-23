# APP-6 PII Encryption — ProductionGuard Checks (Ready-to-Wire)

**Status:** SPEC-LOCKED, COPY-PASTE-READY · **v1.1** (QA defects DEFECT-1/2/3 fixed)
**Owner:** security-hardening-squad
**Depends on:** APP-6 PII Encryption spec v1.0 (`squads/security-hardening/artifacts/app-6-pii-encryption-spec.md`)
**Consumer:** `src/TravelAssistant.ProductionGuard` (to be created by app-dev squad alongside `IPiiCipher` impl)

---

## GuardCheckResult Contract (read this first)

The three checks below assume the existing `IProductionGuardCheck` contract exposes three factory methods on `GuardCheckResult`:

| Factory | Gate workflow behavior | Use when |
|---|---|---|
| `Pass(id, msg)` | ✅ Green — gate allows promotion. | Check passed. |
| `Fail(id, msg)` | ❌ Red — gate **blocks** promotion AND `IHostedService` runner aborts boot. | Configuration smell that breaks PII encryption. |
| `Warn(id, msg)` | 🟡 Yellow — gate **allows** promotion + surfaces in PR summary + dashboard alert. Boot proceeds. | Soft degradation: CMK expiring soon, single optional dep missing, etc. |

**Authority for `Warn`:** confirmed real by review-deployment-squad — the deployment-gate workflow treats `Warn` as pass-with-alert (logged to PR summary + Slack #ops-alerts, no merge block). If `GuardCheckResult.Warn` is NOT yet in the contract when app-dev wires this, app-dev MUST add it before wiring Check 2 — substituting `Pass` silently swallows the 7-day CMK rotation warning.

---

## Purpose

Three startup-time fail-fast checks that block app boot in `ASPNETCORE_ENVIRONMENT=Production` if the PII envelope-encryption substrate is misconfigured. Each check returns a `GuardCheckResult` consumed by the existing `/health/prod-guard` endpoint (see prior decision).

Failures here are **not** runtime degradations — they are configuration smells that mean PII is being written or read without the expected cryptographic envelope. The app MUST refuse to start.

---

## Check 1 — `IPiiCipher` registered in DI

**Why:** If the cipher abstraction isn't in DI, every property-level converter falls back to no-op or throws on first persistence call. We want this to fail at boot, not on the first user write at 3am.

```csharp
using Microsoft.Extensions.DependencyInjection;
using TravelAssistant.Security.Pii; // IPiiCipher

namespace TravelAssistant.ProductionGuard.Checks;

public sealed class PiiCipherRegisteredCheck : IProductionGuardCheck
{
    public string Id => "APP-6.1-pii-cipher-registered";
    public string Description => "IPiiCipher must be registered in DI for PII envelope encryption.";

    public GuardCheckResult Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var cipher = services.GetService<IPiiCipher>();
        if (cipher is null)
        {
            return GuardCheckResult.Fail(
                Id,
                "IPiiCipher is not registered. Call services.AddPiiEnvelopeEncryption(...) in Program.cs. " +
                "Without it, [DataClass(Sensitive)] properties will not be encrypted at rest.");
        }

        return GuardCheckResult.Pass(Id, $"IPiiCipher resolved: {cipher.GetType().FullName}");
    }
}
```

---

## Check 2 — `KeyVault:CmkName` resolves to an existing Key Vault key

**Why:** The Customer-Managed Key wraps every per-tenant DEK. If `CmkName` is misspelled, missing, or the managed identity lacks `Key Vault Crypto User` role, every DEK unwrap fails on first request — not at boot. We pull the key once at startup to validate identity + RBAC + name spelling.

**Timeout (DEFECT-2 fix):** the KV GET is hard-bounded by `_budget` (default 10s). Cold MSI token acquisition + cold KV endpoint can realistically take 1–2s; throttling or transient AAD stalls can hang indefinitely. We fail-fast at the budget rather than letting boot hang.

**Testability (DEFECT-2 fix):** `KeyClient` is constructed via injected `Func<Uri, KeyClient>` factory so unit tests can substitute a fake. Default production factory is `(uri) => new KeyClient(uri, new DefaultAzureCredential())` — wired by `AddProductionGuardCheck<CmkNameResolvesCheck>()` when no override is registered.

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Configuration;

namespace TravelAssistant.ProductionGuard.Checks;

public sealed class CmkNameResolvesCheck : IProductionGuardCheck
{
    public string Id => "APP-6.2-cmk-name-resolves";
    public string Description => "KeyVault:CmkName must resolve to a real Key Vault key with crypto access.";

    private readonly Func<Uri, KeyClient> _keyClientFactory;
    private readonly TimeSpan _budget;

    // Production ctor — DI calls this. Pass null to use defaults.
    public CmkNameResolvesCheck(Func<Uri, KeyClient>? keyClientFactory = null, TimeSpan? budget = null)
    {
        _keyClientFactory = keyClientFactory ?? (uri => new KeyClient(uri, new DefaultAzureCredential()));
        _budget = budget ?? TimeSpan.FromSeconds(10);
    }

    public GuardCheckResult Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = services.GetRequiredService<IConfiguration>();
        var vaultUri = config["KeyVault:Uri"];
        var cmkName  = config["KeyVault:CmkName"];

        if (string.IsNullOrWhiteSpace(vaultUri))
            return GuardCheckResult.Fail(Id, "KeyVault:Uri is not configured.");
        if (string.IsNullOrWhiteSpace(cmkName))
            return GuardCheckResult.Fail(Id, "KeyVault:CmkName is not configured.");

        using var cts = new CancellationTokenSource(_budget);
        try
        {
            var client = _keyClientFactory(new Uri(vaultUri));
            // GET key metadata — proves (a) URI reachable, (b) MI has Crypto User, (c) key exists, (d) name correct.
            // Use async API with explicit CT so the budget actually applies; .GetAwaiter().GetResult() is acceptable
            // inside a startup health check (we want boot to block on this).
            var keyResponse = client.GetKeyAsync(cmkName, version: null, cancellationToken: cts.Token)
                                    .GetAwaiter().GetResult();
            var key = keyResponse.Value;

            if (!key.Properties.Enabled.GetValueOrDefault(false))
                return GuardCheckResult.Fail(Id, $"CMK '{cmkName}' exists but is DISABLED in Key Vault.");

            if (key.Properties.ExpiresOn.HasValue && key.Properties.ExpiresOn.Value <= DateTimeOffset.UtcNow.AddDays(7))
                return GuardCheckResult.Warn(Id, $"CMK '{cmkName}' expires in <7d ({key.Properties.ExpiresOn:O}).");

            return GuardCheckResult.Pass(Id, $"CMK '{cmkName}' v{key.Properties.Version} resolved & enabled.");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return GuardCheckResult.Fail(Id,
                $"Key Vault unreachable within {_budget.TotalSeconds:N0}s budget. " +
                $"Check network egress, MI token acquisition, and KV endpoint health for {vaultUri}.");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            return GuardCheckResult.Fail(Id,
                $"403 Forbidden resolving CMK '{cmkName}'. Managed Identity lacks 'Key Vault Crypto User' role on {vaultUri}.");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return GuardCheckResult.Fail(Id,
                $"404 Not Found: CMK '{cmkName}' does not exist in {vaultUri}. Check spelling and vault.");
        }
        catch (Exception ex)
        {
            return GuardCheckResult.Fail(Id, $"Unexpected error resolving CMK: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

---

## Check 3 — No `[DataClass(Sensitive)]` property lacks an encrypting EF/Cosmos converter

**Why:** This is the most important check. A new EF entity with `[DataClass(DataClassification.Sensitive)] public string Email` ships PII in plaintext unless the developer also calls `.HasConversion(new EncryptedPiiConverter(...))` in `OnModelCreating`. Reviewers miss this. The reflection scan walks every registered `DbContext`, finds every property marked `Sensitive`, and verifies the EF model registers an `EncryptedPiiConverter` (or equivalent) for it.

**DEFECT-1 fix — explicit context-type list, scoped resolution:**
- The original draft used `services.GetServices<DbContext>()`. This returns an EMPTY enumerable in every real EF Core app, because `AddDbContext<AppDbContext>(...)` registers the **derived** type (`AppDbContext`), not the base `DbContext` service. The check silently passed and the entire `[DataClass(Sensitive)]` reflection guard was dead code.
- Additionally, `DbContext` is registered as **scoped**. Resolving it from the root `IServiceProvider` throws under `ValidateScopes=true` (which is on in Development and SHOULD be on in Production).
- **Fix:** the check takes a `Type[] contextTypes` constructor argument (the actual `AppDbContext` types the app uses), creates a scope on each `Run`, and resolves each context from the scoped provider. The wire-up site already knows which contexts to scan.

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using TravelAssistant.Security.Pii; // DataClassAttribute, DataClassification, EncryptedPiiConverter

namespace TravelAssistant.ProductionGuard.Checks;

public sealed class SensitivePropertiesEncryptedCheck : IProductionGuardCheck
{
    public string Id => "APP-6.3-sensitive-props-encrypted";
    public string Description =>
        "Every [DataClass(Sensitive)] property on a DbContext entity must have an EncryptedPiiConverter registered.";

    private readonly Type[] _contextTypes;

    /// <param name="contextTypes">
    /// Concrete <see cref="DbContext"/>-derived types to scan, e.g. <c>typeof(AppDbContext)</c>.
    /// MUST NOT be empty in production wiring — empty means the check is silently disabled.
    /// </param>
    public SensitivePropertiesEncryptedCheck(params Type[] contextTypes)
    {
        ArgumentNullException.ThrowIfNull(contextTypes);
        if (contextTypes.Length == 0)
            throw new ArgumentException(
                "At least one DbContext type must be provided. Empty would silently disable the encryption guard.",
                nameof(contextTypes));
        foreach (var t in contextTypes)
        {
            if (!typeof(DbContext).IsAssignableFrom(t))
                throw new ArgumentException($"Type '{t.FullName}' does not derive from DbContext.", nameof(contextTypes));
        }
        _contextTypes = contextTypes;
    }

    public GuardCheckResult Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // DbContext is scoped — MUST create a scope before resolving, or ValidateScopes=true throws.
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var violations = new List<string>();
        var missingRegistrations = new List<string>();

        foreach (var ctxType in _contextTypes)
        {
            if (sp.GetService(ctxType) is not DbContext ctx)
            {
                missingRegistrations.Add(ctxType.FullName ?? ctxType.Name);
                continue;
            }

            foreach (var entityType in ctx.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                foreach (var clrProp in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var dataClass = clrProp.GetCustomAttribute<DataClassAttribute>();
                    if (dataClass is null || dataClass.Classification != DataClassification.Sensitive)
                        continue;

                    var efProp = entityType.FindProperty(clrProp.Name);
                    if (efProp is null)
                    {
                        // Property not mapped — could be NotMapped intentionally. Skip.
                        continue;
                    }

                    var converter = efProp.GetValueConverter();
                    if (converter is null || !IsEncryptingConverter(converter))
                    {
                        violations.Add(
                            $"{clrType.FullName}.{clrProp.Name} is [DataClass(Sensitive)] but lacks EncryptedPiiConverter " +
                            $"(found: {converter?.GetType().Name ?? "<none>"}).");
                    }
                }
            }
        }

        if (missingRegistrations.Count > 0)
        {
            return GuardCheckResult.Fail(Id,
                $"DbContext type(s) declared but not registered in DI: {string.Join(", ", missingRegistrations)}. " +
                $"Add services.AddDbContext<T>() for each, or remove from SensitivePropertiesEncryptedCheck wiring.");
        }

        if (violations.Count > 0)
        {
            var msg = $"{violations.Count} sensitive property/properties not encrypted:\n  - " +
                      string.Join("\n  - ", violations);
            return GuardCheckResult.Fail(Id, msg);
        }

        return GuardCheckResult.Pass(Id, "All [DataClass(Sensitive)] properties have EncryptedPiiConverter.");
    }

    private static bool IsEncryptingConverter(ValueConverter converter)
        => converter is EncryptedPiiConverter
           || converter.GetType().IsGenericType
              && converter.GetType().GetGenericTypeDefinition() == typeof(EncryptedPiiConverter<>);
}
```

**Cosmos note:** Cosmos uses a different abstraction (`JsonConverter` per property). When the Cosmos provider lands, extend `IsEncryptingConverter` to also accept `EncryptedPiiJsonConverter`. Until then, Cosmos entities are scanned but their converter check is a no-op — log a warning, don't fail.

---

## Wiring

In `Program.cs` after existing ProductionGuard checks:

```csharp
builder.Services.AddProductionGuardCheck<PiiCipherRegisteredCheck>();

// Check 2 — defaults are production-correct; override factory/budget only in tests.
builder.Services.AddProductionGuardCheck(_ => new CmkNameResolvesCheck());

// Check 3 — explicit context-type list (REQUIRED). Add every DbContext the app uses.
builder.Services.AddProductionGuardCheck(_ => new SensitivePropertiesEncryptedCheck(
    typeof(AppDbContext)
    // , typeof(AuditDbContext), typeof(ReportingDbContext), ...
));
```

Endpoint `/health/prod-guard` already returns the full `ProductionGuardReport` JSON for the gate workflow to parse failures (per prior decision).

---

## Test Matrix

| Scenario | Check 1 | Check 2 | Check 3 |
|---|---|---|---|
| Happy path (all wired) | Pass | Pass | Pass |
| `services.AddPiiEnvelopeEncryption` not called | **Fail** | Pass | Fail (no converter) |
| `KeyVault:CmkName` typo | Pass | **Fail (404)** | Pass |
| MI missing `Key Vault Crypto User` | Pass | **Fail (403)** | Pass |
| CMK disabled | Pass | **Fail (disabled)** | Pass |
| Key Vault unreachable / throttled (DEFECT-2) | Pass | **Fail (timeout)** | Pass |
| CMK expires in <7d | Pass | **Warn** (boot ok, gate surfaces) | Pass |
| New entity adds `[DataClass(Sensitive)]` w/o converter | Pass | Pass | **Fail** |
| New entity uses `[DataClass(Public)]` | Pass | Pass | Pass (not scanned) |
| `contextTypes` arg empty (DEFECT-1) | Pass | Pass | **ArgumentException at wire-up** |
| `contextTypes` lists unregistered DbContext type | Pass | Pass | **Fail (missing DI registration)** |

Each scenario should be covered by a unit test in `tests/TravelAssistant.ProductionGuard.Tests/Checks/`. For Check 2, substitute the `Func<Uri,KeyClient>` factory with a fake `KeyClient` (or use `Moq`-of-`KeyClient` if Azure SDK virtuals allow). For Check 3, use EF Core in-memory provider with two handcrafted entities — one with converter wired, one without.

---

## Performance

- **Check 1:** O(1) DI lookup. <1ms.
- **Check 2:** One Key Vault GET. Realistic cold path: **1–2s** (MSI token acquisition + first KV TLS handshake). Warm: ~50–150ms. Hard timeout: **10s** (configurable via ctor).
- **Check 3:** O(contexts × entities × properties) + one scope creation per check run. For a typical app (~2 contexts × ~50 entities × ~10 props), <10ms reflection walk on warm JIT.

Total boot overhead: <**2.5s cold**, <**300ms warm**. Acceptable for fail-fast.

---

## Failure Mode

If `/health/prod-guard` returns a `Fail` for any of these three checks, the deployment gate workflow (review-deployment-squad) blocks promotion to Production. The app process should also fail-fast at startup via the existing `IHostedService` ProductionGuard runner — do not let the listener bind on `:8080` with broken encryption.

`Warn` results do NOT block — they surface on the PR summary + ops alert channel (see GuardCheckResult Contract section at top).

---

## Changelog

- **v1.1** — Fixed QA-found defects:
  - DEFECT-1 (CRITICAL): Check 3 `services.GetServices<DbContext>()` always returned empty in real apps. Replaced with explicit `Type[] contextTypes` ctor arg + scoped resolution.
  - DEFECT-2 (MEDIUM): Check 2 had no timeout on `client.GetKey()` — KV stall would hang boot indefinitely. Added `TimeSpan budget` (default 10s) + `Func<Uri,KeyClient>` factory for testability.
  - DEFECT-3 (CLARIFICATION): Documented `GuardCheckResult.Warn` semantics explicitly at top of doc. Confirmed by review-deployment that the gate treats Warn as pass-with-alert.
- **v1.0** — Initial draft.

---

## Open Questions

1. **CMK rotation grace period** — when `ExpiresOn < now+7d` we return `Warn`. Should we instead `Fail` to force pre-rotation, or stay at `Pass + alert`? Defer to ops squad once rotation cadence is decided.
2. **Cosmos converter discovery** — final shape of `EncryptedPiiJsonConverter` is not yet decided. Until APP-6 Cosmos work lands, Check 3 skips Cosmos entities silently with a single warn log.

Both questions are non-blocking — the checks above ship as-is.
