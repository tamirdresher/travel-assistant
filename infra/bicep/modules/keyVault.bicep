@description('Key Vault name')
param keyVaultName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('Managed Identity Principal ID of the Container App (for access policy)')
param containerAppManagedIdentityPrincipalId string

// Key Vault - Standard tier (no Premium HSM needed for MVP)
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard' // Standard tier is sufficient for secret storage
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: false // Use access policies for simplicity
    enableSoftDelete: true
    softDeleteRetentionInDays: 7 // Minimum retention for soft delete
    enablePurgeProtection: false // Not required for MVP, saves cost
    publicNetworkAccess: 'Enabled'
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: containerAppManagedIdentityPrincipalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
          // No key or certificate permissions needed for MVP
        }
      }
    ]
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
