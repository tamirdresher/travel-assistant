# SEC-1 — Secrets Policy & Key Vault from Day One

**Owner:** Ripley · **Blocks:** all deploys

## Policy (hard rules)

1. **No secrets in source.** No connection strings, API keys, tokens,
   passwords, signing keys, or client secrets in `appsettings*.json`,
   Bicep parameter files, `.env`, Dockerfiles, or workflow YAML.
   Public configuration (feature flags, allowlists, region names) is fine.
2. **All secrets resolve via managed identity → Azure Key Vault.**
   App code reads via `AddAzureKeyVault` against
   `https://<env>-travelassistant-kv.vault.azure.net/` using the
   Container App's system-assigned managed identity. No connection
   strings, no SAS, no shared keys.
3. **No secret rotation TODOs in code.** If a third-party requires a
   long-lived key, wrap it in a Key Vault reference and document the
   rotation cadence in this file (currently: none).
4. **CI fails on detection.** `gitleaks` runs on every PR and push to
   `main`. Findings block merge.
5. **Local dev uses user-secrets**, never the repo. `dotnet user-secrets`
   is the only acceptable store for a developer's personal API keys.

## Reference Bicep
See `infra/modules/keyvault.bicep` — soft-delete on, purge protection on,
RBAC authorization (no access policies), private endpoint ready.

## Reference Program.cs snippet
```csharp
if (!builder.Environment.IsDevelopment())
{
    var kvUri = builder.Configuration["KeyVault:Uri"]
        ?? throw new InvalidOperationException("KeyVault:Uri required outside Development.");
    builder.Configuration.AddAzureKeyVault(
        new Uri(kvUri),
        new DefaultAzureCredential());
}
```

## CI enforcement
`.github/workflows/secret-scan.yml` runs gitleaks. See workflow file for
the exact rule set. Bypass requires a `security:approved-leak` label
applied by Ripley *and* an entry in `docs/security/sec-1/exceptions.md`
(file deliberately absent — there are zero exceptions today).

## Acceptance criteria (from SEC-1)
- [x] No keys in `appsettings.json` (verified — only `Auth:TrustedProxyCidrs`)
- [x] Key Vault references via managed identity (`Program.cs` snippet + Bicep)
- [x] CI fails if a connection string or API key is detected
  (`.github/workflows/secret-scan.yml` + `.gitleaks.toml`)
