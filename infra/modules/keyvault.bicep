// SEC-1 — Azure Key Vault module. Soft-delete + purge protection ON.
// RBAC authorization (no legacy access policies). Designed to be
// consumed by infra squad's env templates (dev / stage / prod).
//
// Owner: security-hardening-squad (Ripley). Wire-in owner: azure-infrastructure-squad.

@description('Key Vault name. Must be globally unique, 3-24 chars, alphanumeric and hyphens.')
@minLength(3)
@maxLength(24)
param name string

@description('Azure region for the vault.')
param location string = resourceGroup().location

@description('Tags applied to the vault.')
param tags object = {
  app: 'travel-assistant'
}

@description('Object ID of the workload managed identity that needs secret read access.')
param workloadPrincipalId string

@description('Object ID of the deployment principal (azd / human ops) that needs secret write.')
param deployPrincipalId string = ''

@description('Enable private endpoint. Set false only in dev sandboxes.')
param enablePrivateEndpoint bool = true

@description('Subnet resource id for the private endpoint. Required if enablePrivateEndpoint = true.')
param privateEndpointSubnetId string = ''

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
    networkAcls: {
      defaultAction: enablePrivateEndpoint ? 'Deny' : 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource workloadReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, workloadPrincipalId, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    principalId: workloadPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

resource deployWriter 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployPrincipalId)) {
  name: guid(vault.id, deployPrincipalId, keyVaultSecretsOfficerRoleId)
  scope: vault
  properties: {
    principalId: deployPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRoleId)
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoint) {
  name: '${name}-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${name}-plsc'
        properties: {
          privateLinkServiceId: vault.id
          groupIds: [ 'vault' ]
        }
      }
    ]
  }
}

output vaultUri string = vault.properties.vaultUri
output vaultId string = vault.id
output vaultName string = vault.name
