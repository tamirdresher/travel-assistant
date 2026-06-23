using '../main.bicep'

param environmentName = 'prod'
param resourcePrefix = 'travelassist'
param location = 'eastus'
param tags = {
  environment: 'prod'
  project: 'travel-assistant'
  managedBy: 'bicep'
  owner: 'squad'
}

param refreshTokenSigningKey = readEnvironmentVariable('AZURE_REFRESH_TOKEN_SIGNING_KEY', '')
// Prod: 30d sliding, 90d absolute. Reduce if abuse signal from auth.refresh.long_lived.issued spikes.
param refreshTokenLongTtlSeconds = 2592000
param refreshTokenAbsoluteCapSeconds = 7776000
param authCookieDomain = '.travel-assistant.example.com'
