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

@description('Refresh token signing key (HMAC). Pass via pipeline secret or generate with: openssl rand -base64 64')
@secure()
param refreshTokenSigningKey string

@description('Long-lived ("remember me") refresh token TTL in seconds. Default 30 days.')
param refreshTokenLongTtlSeconds int = 2592000

@description('Absolute cap on refresh token family lifetime in seconds. Default 90 days per RM-005.')
param refreshTokenAbsoluteCapSeconds int = 7776000

@description('Auth cookie domain for this environment (e.g., .dev.travel-assistant.example.com)')
param authCookieDomain string

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

// 4b. Auth refresh-token secrets (depends on Key Vault)
//     Owns: signing key, standard TTL, long-lived TTL (remember-me), cookie domain, SameSite.
//     Browser-side telemetry stays deferred per DM-006; server-side OTel counters live in API code.
module authSecrets './modules/authSecrets.bicep' = {
  name: 'authSecrets-deployment'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    refreshTokenSigningKey: refreshTokenSigningKey
    refreshTokenLongTtlSeconds: refreshTokenLongTtlSeconds
    refreshTokenAbsoluteCapSeconds: refreshTokenAbsoluteCapSeconds
    authCookieDomain: authCookieDomain
    authCookieSameSite: 'Lax'
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
output authRefreshTokenSigningKeySecretUri string = authSecrets.outputs.signingKeySecretUri
output authRefreshTokenLongTtlSecretUri string = authSecrets.outputs.longTtlSecretUri
output authRefreshTokenAbsoluteCapSecretUri string = authSecrets.outputs.absoluteCapSecretUri
output authCookieDomainSecretUri string = authSecrets.outputs.cookieDomainSecretUri
output authCookieNameSecretUri string = authSecrets.outputs.cookieNameSecretUri
output authCookiePathSecretUri string = authSecrets.outputs.cookiePathSecretUri
