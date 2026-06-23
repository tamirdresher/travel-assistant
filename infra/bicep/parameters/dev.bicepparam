using './main.bicep'

param environmentName = 'dev'
param resourcePrefix = 'travelassist'
param location = 'eastus'
param tags = {
  environment: 'dev'
  project: 'travel-assistant'
  managedBy: 'bicep'
  owner: 'squad'
}

// Refresh-token signing key MUST come from pipeline secret (env var), never committed.
// Example pipeline: AZURE_REFRESH_TOKEN_SIGNING_KEY="$(openssl rand -base64 64)" az deployment ...
param refreshTokenSigningKey = readEnvironmentVariable('AZURE_REFRESH_TOKEN_SIGNING_KEY', '')

// 30 days for remember-me. Tune per environment.
param refreshTokenLongTtlSeconds = 2592000

// Dev apex. Preview/prod params files override.
param authCookieDomain = '.dev.travel-assistant.local'
