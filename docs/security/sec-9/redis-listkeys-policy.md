# SEC-9 · Redis `listKeys` deny policy — operator runbook

Owner: security-hardening squad
Status: v1.0 (2026-06-23)
Module: [`infra/bicep/policies/redis-deny-listkeys.bicep`](../../../infra/bicep/policies/redis-deny-listkeys.bicep)

## Why this exists

Azure Cache for Redis Basic/Standard SKUs have **no API** to disable access-key
authentication. The `aad-enabled=true` setting wired by
`infra/bicep/modules/redis.bicep` enables AAD as an **additional** auth method
— the access keys remain valid and any caller holding
`Microsoft.Cache/redis/listKeys/action` (built-in role *Azure Cache for Redis
Contributor*, or Owner/Contributor at any parent scope) can fetch them.

On the **Premium** SKU we set `properties.disableAccessKeyAuthentication: true`
in the module directly; this policy is a defence-in-depth layer there.

Azure-infra's `docs/security/sec-3/redis-residual-key-risk.md` names this
policy as the canonical mitigation for the Basic/Standard residual-key risk.

## What it does

Denies any call to:
- `Microsoft.Cache/redis/listKeys/action`
- `Microsoft.Cache/redis/regenerateKey/action`

…unless the calling principal's **object ID** is in the
`breakGlassPrincipalIds` parameter (intended to be empty in steady-state).

## Deploy

Per resource group containing Redis resources (one assignment per env):

```bash
RG=rg-travel-assistant-staging   # repeat for prod

az deployment group create \
  --resource-group "$RG" \
  --template-file infra/bicep/policies/redis-deny-listkeys.bicep \
  --parameters auditOnly=true                # first 24h only
```

After 24h of clean audit logs, redeploy with `auditOnly=false` (or omit — Deny
is the default). The two-phase rollout catches any legitimate caller you
forgot — operator runbooks, on-call scripts, anything pinned to the keys.

## Verify

```bash
# Confirm denial from a non-break-glass principal
az redis list-keys --name <redis-name> --resource-group "$RG"
# expected: RequestDisallowedByPolicy referencing sec-9-redis-deny-listkeys
```

Activity log entries land under `Microsoft.Authorization/policies/audit/action`
with the policy's display name.

## Break-glass

For incident response only:

1. Get the responder's AAD object ID (`az ad signed-in-user show --query id -o tsv`).
2. Redeploy the policy with `breakGlassPrincipalIds=["<objectId>"]`.
3. Do the work. Capture the activity log evidence.
4. **Revert** by redeploying with the parameter empty. Maximum dwell time 4h.
5. Rotate the Redis keys (`az redis regenerate-keys`) once break-glass closes,
   regardless of whether they were read.

Break-glass deployments should be logged in `decisions.md` with the responder,
time window, and reason.

## Relationship to other controls

| Control | Where | What it covers |
| --- | --- | --- |
| `properties.disableAccessKeyAuthentication: true` | `redis.bicep`, Premium only | Hard-disables keys at the data plane (no listKeys can succeed because no keys exist). |
| AAD (`aad-enabled=true`) | `redis.bicep`, all SKUs | Adds AAD auth as an option for runtime callers (Container Apps MIs). |
| **This policy (SEC-9)** | `redis-deny-listkeys.bicep`, all SKUs | Stops the control-plane key-extraction path. Compensating control for B/S; defence-in-depth for P. |
| Audit-trail | Activity log | Records every denied attempt for forensics. |

## Acceptance

- [x] Module deploys clean on a fresh RG.
- [x] `az redis list-keys` from a non-break-glass principal returns
  `RequestDisallowedByPolicy`.
- [x] Audit-only mode flips back to Deny without recreating the assignment.
- [ ] Wired into `main.bicep` post-INF-bundle merge (caller responsibility, not
  this PR — `main.bicep` lives in azure-infra's tree).
