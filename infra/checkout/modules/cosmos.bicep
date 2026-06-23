param env string
param location string
@minLength(2)
param namePrefix string
param tags object
param peSubnetId string
param privateDnsZoneId string
param appPrincipalId string

var workload = 'checkout'
var resourceBase = toLower('${namePrefix}-${env}-${workload}')
var accountName = take('${replace(resourceBase, '-', '')}${uniqueString(resourceGroup().id, resourceBase)}', 44)

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    publicNetworkAccess: 'Disabled'
    disableLocalAuth: true
    enableFreeTier: env == 'dev'
    enableAutomaticFailover: env == 'prod'
    minimalTlsVersion: 'Tls12'
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: env == 'prod'
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: account
  name: 'checkout'
  properties: {
    resource: {
      id: 'checkout'
    }
  }
}

resource orders 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'orders'
  properties: {
    resource: {
      id: 'orders'
      partitionKey: {
        paths: [
          '/orderId'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource idempotency 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'idempotency'
  properties: {
    resource: {
      id: 'idempotency'
      partitionKey: {
        paths: [
          '/key'
        ]
        kind: 'Hash'
      }
      defaultTtl: 86400
    }
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${resourceBase}-cosmos-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'cosmos-sql'
        properties: {
          privateLinkServiceId: account.id
          groupIds: [
            'Sql'
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
        name: 'cosmos-sql'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

resource dataContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: account
  name: guid(account.id, appPrincipalId, 'Cosmos DB Built-in Data Contributor')
  properties: {
    principalId: appPrincipalId
    roleDefinitionId: '${account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: account.id
  }
}

output accountName string = account.name
output accountId string = account.id
output privateEndpointName string = privateEndpoint.name
