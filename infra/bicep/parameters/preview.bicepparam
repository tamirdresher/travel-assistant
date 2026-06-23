using '../main.bicep'

param environmentName = 'preview'
param resourcePrefix = 'travelassist'
param location = 'eastus'
param tags = {
  environment: 'preview'
  project: 'travel-assistant'
  managedBy: 'bicep'
  owner: 'squad'
}

param refreshTokenSigningKey = readEnvironmentVariable('AZURE_REFRESH_TOKEN_SIGNING_KEY', '')
param refreshTokenLongTtlSeconds = 2592000
param refreshTokenAbsoluteCapSeconds = 7776000
param authCookieDomain = '.preview.travel-assistant.example.com'
