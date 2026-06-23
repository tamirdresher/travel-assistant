using '../main.bicep'

param env = 'prod'
param location = 'eastus2'
param namePrefix = 'ta'
param minReplicas = 2
param maxReplicas = 20
param containerImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param allowedFrontDoorCountries = []
