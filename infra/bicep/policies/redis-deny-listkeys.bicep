// SEC-9 · Azure Policy: deny `listKeys` on Redis Cache for non-break-glass principals.
//
// CONTEXT
// =======
// Azure Cache for Redis Basic/Standard SKUs have NO API to disable access-key
// authentication. `aad-enabled=true` (set in `redis.bicep`) merely *adds* AAD as
// an additional auth method — the original access keys remain valid, and anyone
// holding a role that grants `Microsoft.Cache/redis/listKeys/action` (e.g. the
// built-in `Azure Cache for Redis Contributor`, role GUID e60a18e1-...,  or
// Owner / Contributor at any scope above the resource) can fetch them.
//
// On the Premium SKU the canonical control is
// `properties.disableAccessKeyAuthentication: true` (also wired by
// `redis.bicep` via the `disableAccessKeyAuthentication` param). This policy is
// the COMPENSATING control for Basic/Standard — and a defence-in-depth layer
// on top of Premium in case the SKU is downgraded.
//
// POLICY EFFECT
// =============
// Deny (not Audit) any request to `Microsoft.Cache/redis/listKeys/action` and
// `Microsoft.Cache/redis/regenerateKey/action` unless the calling principal's
// objectId is in `breakGlassPrincipalIds`. Break-glass list intended to be
// empty in steady-state; populated only for ad-hoc incident response.
//
// SCOPING
// =======
// Assigned at the resource group containing the Redis resources (one assignment
// per env RG). Module returns the policy-definition + assignment resource IDs.
//
// REFERENCES
// ==========
// - SEC-9 backlog item (security-hardening squad).
// - docs/security/sec-9/redis-listkeys-policy.md — operator runbook.
// - docs/security/sec-3/redis-residual-key-risk.md (azure-infra) — mitigation
//   pointer that names this policy as the Basic/Standard control.

targetScope = 'resourceGroup'

@description('Display name shown in the Azure Policy blade. Keep stable across envs for assignment-rollup reporting.')
param policyName string = 'sec-9-redis-deny-listkeys'

@description('Object IDs (NOT app IDs) of principals permitted to call listKeys/regenerateKey. Keep empty in steady-state; populate only for incident response and revert.')
param breakGlassPrincipalIds array = []

@description('Set to true to switch the policy effect to Audit instead of Deny. Use ONLY for the first 24h after rollout to catch unexpected legitimate callers, then revert to Deny.')
param auditOnly bool = false

var policyEffect = auditOnly ? 'Audit' : 'Deny'

resource policyDefinition 'Microsoft.Authorization/policyDefinitions@2023-04-01' = {
  name: policyName
  properties: {
    displayName: 'SEC-9 · Deny listKeys/regenerateKey on Azure Cache for Redis'
    description: 'Blocks Microsoft.Cache/redis/listKeys/action and regenerateKey/action for any principal not listed in breakGlassPrincipalIds. Compensating control for Redis Basic/Standard SKUs where disableAccessKeyAuthentication is unavailable. Required by SEC-9; references SEC-3 redis-residual-key-risk.md.'
    policyType: 'Custom'
    mode: 'All'
    metadata: {
      category: 'Cache'
      version: '1.0.0'
      source: 'security-hardening-squad'
    }
    parameters: {
      effect: {
        type: 'String'
        allowedValues: [ 'Deny', 'Audit', 'Disabled' ]
        defaultValue: policyEffect
        metadata: {
          displayName: 'Effect'
          description: 'Deny (default) blocks the call; Audit logs it; Disabled turns the policy off.'
        }
      }
      breakGlassPrincipalIds: {
        type: 'Array'
        defaultValue: breakGlassPrincipalIds
        metadata: {
          displayName: 'Break-glass principal object IDs'
          description: 'Principals exempt from the deny. Should be empty in steady-state.'
        }
      }
    }
    policyRule: {
      if: {
        allOf: [
          {
            field: 'type'
            equals: 'Microsoft.Cache/redis'
          }
          {
            anyOf: [
              {
                field: 'Microsoft.Authorization/roleAssignments/permissions[*].actions[*]'
                equals: 'Microsoft.Cache/redis/listKeys/action'
              }
              {
                field: 'Microsoft.Authorization/roleAssignments/permissions[*].actions[*]'
                equals: 'Microsoft.Cache/redis/regenerateKey/action'
              }
            ]
          }
          {
            not: {
              value: '[current(\'principalId\')]'
              in: '[parameters(\'breakGlassPrincipalIds\')]'
            }
          }
        ]
      }
      then: {
        effect: '[parameters(\'effect\')]'
      }
    }
  }
}

resource policyAssignment 'Microsoft.Authorization/policyAssignments@2023-04-01' = {
  name: '${policyName}-assign'
  location: resourceGroup().location
  properties: {
    displayName: 'SEC-9 · Deny listKeys on Redis (RG-scoped)'
    description: 'Assigns ${policyName} at resource group ${resourceGroup().name}. Owned by security-hardening squad.'
    policyDefinitionId: policyDefinition.id
    enforcementMode: 'Default'
    parameters: {
      effect: {
        value: policyEffect
      }
      breakGlassPrincipalIds: {
        value: breakGlassPrincipalIds
      }
    }
    nonComplianceMessages: [
      {
        message: 'Calls to Redis listKeys/regenerateKey are denied by SEC-9. Use the AAD path (aad-enabled=true) or, for incident response, request break-glass via the SEC-9 runbook at docs/security/sec-9/redis-listkeys-policy.md.'
      }
    ]
  }
}

output policyDefinitionId string = policyDefinition.id
output policyAssignmentId string = policyAssignment.id
output effectInUse string = policyEffect
