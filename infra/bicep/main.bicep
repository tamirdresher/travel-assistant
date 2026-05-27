targetScope = 'resourceGroup'

@description('Environment name (e.g., dev, staging, prod)')
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Resource prefix for naming (e.g., travelassist)')
@maxLength(12)
param resourcePrefix string

@description('Tags to apply to all resources')
param tags object = {
  environment: environmentName
  project: 'travel-assistant'
  managedBy: 'bicep'
}

// Outputs from modules
var logWorkspaceName = '${resourcePrefix}-${environmentName}-logs'
var appInsightsName = '${resourcePrefix}-${environmentName}-ai'
var containerAppsEnvName = '${resourcePrefix}-${environmentName}-cae'
var cosmosAccountName = '${resourcePrefix}-${environmentName}-cosmos'
var keyVaultName = '${resourcePrefix}-${environmentName}-kv'
var staticWebAppName = '${resourcePrefix}-${environmentName}-swa'

// 1. Log Analytics Workspace + Application Insights (foundation for observability)
module appInsights './modules/appInsights.bicep' = {
  name: 'appInsights-deployment'
  params: {
    logWorkspaceName: logWorkspaceName
    appInsightsName: appInsightsName
    location: location
    tags: tags
  }
}

// 2. Container Apps Environment (depends on Log Analytics)
module containerApps './modules/containerApps.bicep' = {
  name: 'containerApps-deployment'
  params: {
    containerAppsEnvName: containerAppsEnvName
    location: location
    tags: tags
    logWorkspaceId: appInsights.outputs.logWorkspaceId
  }
}

// 3. Cosmos DB (serverless NoSQL for conversation history + user profiles)
module cosmosDb './modules/cosmosDb.bicep' = {
  name: 'cosmosDb-deployment'
  params: {
    accountName: cosmosAccountName
    location: location
    tags: tags
  }
}

// 4. Key Vault (for storing secrets like Amadeus API key, Azure OpenAI key)
module keyVault './modules/keyVault.bicep' = {
  name: 'keyVault-deployment'
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
    containerAppManagedIdentityPrincipalId: containerApps.outputs.apiAppManagedIdentityPrincipalId
  }
}

// 5. Static Web App (free tier for Next.js frontend)
module staticWebApp './modules/staticWebApp.bicep' = {
  name: 'staticWebApp-deployment'
  params: {
    staticWebAppName: staticWebAppName
    location: location
    tags: tags
  }
}

// Main outputs
output logWorkspaceId string = appInsights.outputs.logWorkspaceId
output appInsightsInstrumentationKey string = appInsights.outputs.instrumentationKey
output appInsightsConnectionString string = appInsights.outputs.connectionString
output containerAppsEnvId string = containerApps.outputs.containerAppsEnvId
output apiAppFqdn string = containerApps.outputs.apiAppFqdn
output apiAppManagedIdentityPrincipalId string = containerApps.outputs.apiAppManagedIdentityPrincipalId
output cosmosAccountEndpoint string = cosmosDb.outputs.accountEndpoint
output cosmosAccountName string = cosmosDb.outputs.accountName
output cosmosDatabaseName string = cosmosDb.outputs.databaseName
output cosmosContainerName string = cosmosDb.outputs.containerName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output keyVaultName string = keyVault.outputs.keyVaultName
output staticWebAppDefaultHostname string = staticWebApp.outputs.defaultHostname
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
