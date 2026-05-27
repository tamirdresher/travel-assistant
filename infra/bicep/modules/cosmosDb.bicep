@description('Cosmos DB account name')
param accountName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('Database name')
param databaseName string = 'travelassistant'

@description('Container name for conversation history')
param containerName string = 'conversations'

// Cosmos DB Account - serverless NoSQL for cost optimization
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session' // Good balance for chat apps
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false // Cost optimization
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless' // Serverless tier: pay-per-request, no provisioned throughput
      }
    ]
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 1440 // Daily backups
        backupRetentionIntervalInHours: 168 // 7 days retention
      }
    }
    enableFreeTier: false // Free tier is account-level, not recommended for production
    publicNetworkAccess: 'Enabled'
  }
}

// Database
resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

// Container for conversation history
resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: {
        paths: [
          '/userId' // Partition by userId for efficient queries per user
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?' // Exclude system properties
          }
        ]
      }
      defaultTtl: 7776000 // 90 days TTL for conversation history (auto-delete old data)
    }
  }
}

output accountId string = cosmosAccount.id
output accountName string = cosmosAccount.name
output accountEndpoint string = cosmosAccount.properties.documentEndpoint
output databaseName string = database.name
output containerName string = container.name
