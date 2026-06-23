param env string
param location string
@minLength(2)
param namePrefix string
param tags object
param vaultName string
param peSubnetId string
param privateDnsZoneId string
param appPrincipalId string

var workload = 'checkout'
var resourceBase = toLower('${namePrefix}-${env}-${workload}')
var secretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    publicNetworkAccess: 'Disabled'
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

resource paymentApiKey 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: vault
  name: 'payment-provider-api-key'
  properties: {
    value: 'placeholder-rotate-out-of-band'
    contentType: 'text/plain'
  }
}

resource webhookSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: vault
  name: 'payment-provider-webhook-secret'
  properties: {
    value: 'placeholder-rotate-out-of-band'
    contentType: 'text/plain'
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${resourceBase}-kv-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'keyvault-vault'
        properties: {
          privateLinkServiceId: vault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'keyvault'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

resource appSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, appPrincipalId, 'Key Vault Secrets User')
  scope: vault
  properties: {
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: secretsUserRoleId
  }
}

output vaultName string = vault.name
output vaultId string = vault.id
output paymentApiKeySecretUri string = paymentApiKey.properties.secretUri
output webhookSecretUri string = webhookSecret.properties.secretUri
output privateEndpointName string = privateEndpoint.name
