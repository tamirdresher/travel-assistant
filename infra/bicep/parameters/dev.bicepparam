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
