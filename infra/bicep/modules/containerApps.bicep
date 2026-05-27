@description('Container Apps Environment name')
param containerAppsEnvName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('Log Analytics Workspace ID')
param logWorkspaceId string

// Container Apps Environment
resource containerAppsEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logWorkspaceId, '2022-10-01').customerId
        sharedKey: listKeys(logWorkspaceId, '2022-10-01').primarySharedKey
      }
    }
    zoneRedundant: false // Cost optimization: no zone redundancy for MVP
  }
}

// Placeholder API Container App (consumption tier, scale-to-zero)
resource apiApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${containerAppsEnvName}-api'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned' // Managed identity for Key Vault access
  }
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: []
    }
    template: {
      containers: [
        {
          name: 'api-placeholder'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' // Placeholder; Peres will replace
          resources: {
            cpu: json('0.25') // Consumption tier: 0.25-2.0 vCPU
            memory: '0.5Gi' // 0.5-4.0 GB
          }
        }
      ]
      scale: {
        minReplicas: 0 // Scale to zero for cost savings
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

output containerAppsEnvId string = containerAppsEnv.id
output containerAppsEnvName string = containerAppsEnv.name
output apiAppId string = apiApp.id
output apiAppName string = apiApp.name
output apiAppFqdn string = apiApp.properties.configuration.ingress.fqdn
output apiAppManagedIdentityPrincipalId string = apiApp.identity.principalId
