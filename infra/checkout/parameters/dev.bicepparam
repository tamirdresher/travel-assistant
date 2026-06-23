using '../main.bicep'

param env = 'dev'
param location = 'eastus2'
param namePrefix = 'ta'
param minReplicas = 1
param maxReplicas = 5
param containerImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param allowedFrontDoorCountries = []
