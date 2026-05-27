@description('Static Web App name')
param staticWebAppName string

@description('Azure region (Static Web Apps have limited region support)')
param location string

@description('Resource tags')
param tags object

// Static Web App - Free tier for Next.js frontend
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  tags: tags
  sku: {
    name: 'Free' // Free tier: 100 GB bandwidth/month, custom domains, SSL
    tier: 'Free'
  }
  properties: {
    repositoryUrl: '' // Will be configured via GitHub Actions or manual setup
    branch: '' // Will be configured via GitHub Actions
    buildProperties: {
      appLocation: 'apps/web' // Next.js app location in repo
      apiLocation: '' // No Azure Functions API for this SWA
      outputLocation: '.next' // Next.js build output
    }
    stagingEnvironmentPolicy: 'Enabled' // Enable staging environments for PR previews
    allowConfigFileUpdates: true
    provider: 'GitHub'
  }
}

output staticWebAppId string = staticWebApp.id
output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
output repositoryUrl string = staticWebApp.properties.repositoryUrl
