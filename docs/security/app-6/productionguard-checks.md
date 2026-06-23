# APP-6 PII Encryption — ProductionGuard Checks (Ready-to-Wire)

**Status:** SPEC-LOCKED, COPY-PASTE-READY
**Owner:** security-hardening-squad
**Depends on:** APP-6 PII Encryption spec v1.0 (`squads/security-hardening/artifacts/app-6-pii-encryption-spec.md`)
**Consumer:** `src/TravelAssistant.ProductionGuard` (to be created by app-dev squad alongside `IPiiCipher` impl)

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

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Configuration;

namespace TravelAssistant.ProductionGuard.Checks;

public sealed class CmkNameResolvesCheck : IProductionGuardCheck
{
    public string Id => "APP-6.2-cmk-name-resolves";
    public string Description => "KeyVault:CmkName must resolve to a real Key Vault key with crypto access.";

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

        try
        {
            var client = new KeyClient(new Uri(vaultUri), new DefaultAzureCredential());
            // GET key metadata — proves (a) URI reachable, (b) MI has Crypto User, (c) key exists, (d) name correct.
            var keyResponse = client.GetKey(cmkName);
            var key = keyResponse.Value;

            if (!key.Properties.Enabled.GetValueOrDefault(false))
                return GuardCheckResult.Fail(Id, $"CMK '{cmkName}' exists but is DISABLED in Key Vault.");

            if (key.Properties.ExpiresOn.HasValue && key.Properties.ExpiresOn.Value <= DateTimeOffset.UtcNow.AddDays(7))
                return GuardCheckResult.Warn(Id, $"CMK '{cmkName}' expires in <7d ({key.Properties.ExpiresOn:O}).");

            return GuardCheckResult.Pass(Id, $"CMK '{cmkName}' v{key.Properties.Version} resolved & enabled.");
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

**Why:** This is the most important check. A new EF entity with `[DataClass(DataClassification.Sensitive)] public string Email` ships PII in plaintext unless the developer also calls `.HasConversion(new EncryptedPiiConverter(...))` in `OnModelCreating`. Reviewers miss this. The reflection scan walks every loaded assembly's `DbContext` types, finds every property marked `Sensitive`, and verifies the EF model registers an `EncryptedPiiConverter` (or equivalent) for it.

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TravelAssistant.Security.Pii; // DataClassAttribute, DataClassification, EncryptedPiiConverter

namespace TravelAssistant.ProductionGuard.Checks;

public sealed class SensitivePropertiesEncryptedCheck : IProductionGuardCheck
{
    public string Id => "APP-6.3-sensitive-props-encrypted";
    public string Description =>
        "Every [DataClass(Sensitive)] property on a DbContext entity must have an EncryptedPiiConverter registered.";

    public GuardCheckResult Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var contexts = services.GetServices<DbContext>().ToList();
        if (contexts.Count == 0)
            return GuardCheckResult.Pass(Id, "No DbContext registered — nothing to scan.");

        var violations = new List<string>();

        foreach (var ctx in contexts)
        {
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
                        // Property not mapped — log but don't fail (could be NotMapped intentionally).
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
builder.Services.AddProductionGuardCheck<CmkNameResolvesCheck>();
builder.Services.AddProductionGuardCheck<SensitivePropertiesEncryptedCheck>();
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
| New entity adds `[DataClass(Sensitive)]` w/o converter | Pass | Pass | **Fail** |
| New entity uses `[DataClass(Public)]` | Pass | Pass | Pass (not scanned) |

Each scenario should be covered by a unit test in `tests/TravelAssistant.ProductionGuard.Tests/Checks/`. Mock `IServiceProvider` + an in-memory `DbContext` for Check 3.

---

## Performance

- Check 1: O(1) — DI lookup.
- Check 2: One Key Vault GET (~50–200ms cold, cached on subsequent boots within same instance).
- Check 3: O(entities × properties). For a typical app (~50 entities × ~10 props), <5ms reflection walk on a warm JIT.

Total boot overhead: <300ms. Acceptable for fail-fast.

---

## Failure Mode

If `/health/prod-guard` returns a `Fail` for any of these three checks, the deployment gate workflow (review-deployment-squad) blocks promotion to Production. The app process should also fail-fast at startup via the existing `IHostedService` ProductionGuard runner — do not let the listener bind on `:8080` with broken encryption.

---

## Open Questions

1. **CMK rotation grace period** — when `ExpiresOn < now+7d` we return `Warn`. Should we instead `Fail` to force pre-rotation, or `Pass + alert`? Defer to ops squad once rotation cadence is decided.
2. **Cosmos converter discovery** — final shape of `EncryptedPiiJsonConverter` is not yet decided. Until APP-6 Cosmos work lands, Check 3 skips Cosmos entities silently with a single warn log.

Both questions are non-blocking — the checks above ship as-is.
