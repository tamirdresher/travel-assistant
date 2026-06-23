targetScope = 'subscription'

@allowed([
  'dev'
  'prod'
])
param env string = 'dev'

param location string = deployment().location
@minLength(2)
param namePrefix string = 'ta'
param containerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@minValue(0)
param minReplicas int = env == 'prod' ? 2 : 1

@minValue(1)
param maxReplicas int = env == 'prod' ? 20 : 5

param allowedFrontDoorCountries array = []

var workload = 'checkout'
var resourceBase = toLower('${namePrefix}-${env}-${workload}')
var tags = {
  workload: workload
  env: env
  managedBy: 'bicep'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: '${resourceBase}-rg'
  location: location
  tags: tags
}

module network 'modules/network.bicep' = {
  name: '${resourceBase}-network'
  scope: rg
  params: {
    env: env
    location: location
    namePrefix: namePrefix
    tags: tags
  }
}

var keyVaultName = take(replace('${resourceBase}-kv', '-', ''), 24)
var keyVaultDnsSuffix = environment().suffixes.keyvaultDns
var paymentApiKeySecretUri = 'https://${keyVaultName}.${keyVaultDnsSuffix}/secrets/payment-provider-api-key'
var webhookSecretUri = 'https://${keyVaultName}.${keyVaultDnsSuffix}/secrets/payment-provider-webhook-secret'

module containerApp 'modules/containerApp.bicep' = {
  name: '${resourceBase}-containerapp'
  scope: rg
  params: {
    env: env
    location: location
    namePrefix: namePrefix
    tags: tags
    acaSubnetId: network.outputs.acaSubnetId
    containerImage: containerImage
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    paymentApiKeySecretUri: paymentApiKeySecretUri
    webhookSecretUri: webhookSecretUri
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: '${resourceBase}-keyvault'
  scope: rg
  params: {
    env: env
    location: location
    namePrefix: namePrefix
    tags: tags
    vaultName: keyVaultName
    peSubnetId: network.outputs.privateEndpointSubnetId
    privateDnsZoneId: network.outputs.keyVaultPrivateDnsZoneId
    appPrincipalId: containerApp.outputs.principalId
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: '${resourceBase}-cosmos'
  scope: rg
  params: {
    env: env
    location: location
    namePrefix: namePrefix
    tags: tags
    peSubnetId: network.outputs.privateEndpointSubnetId
    privateDnsZoneId: network.outputs.cosmosPrivateDnsZoneId
    appPrincipalId: containerApp.outputs.principalId
  }
}

module serviceBus 'modules/serviceBus.bicep' = {
  name: '${resourceBase}-servicebus'
  scope: rg
  params: {
    env: env
    location: location
    namePrefix: namePrefix
    tags: tags
    peSubnetId: network.outputs.privateEndpointSubnetId
    privateDnsZoneId: network.outputs.serviceBusPrivateDnsZoneId
    appPrincipalId: containerApp.outputs.principalId
  }
}

module frontDoor 'modules/frontDoor.bicep' = {
  name: '${resourceBase}-frontdoor'
  scope: rg
  params: {
    env: env
    location: 'global'
    namePrefix: namePrefix
    tags: tags
    originHostName: containerApp.outputs.fqdn
    allowedCountryCodes: allowedFrontDoorCountries
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: '${resourceBase}-monitoring'
  scope: rg
  params: {
    env: env
    location: location
    namePrefix: namePrefix
    tags: tags
    containerAppName: containerApp.outputs.name
    containerAppEnvironmentName: containerApp.outputs.environmentName
    cosmosAccountName: cosmos.outputs.accountName
    serviceBusNamespaceName: serviceBus.outputs.namespaceName
    keyVaultName: keyVault.outputs.vaultName
    frontDoorProfileName: frontDoor.outputs.profileName
    wafPolicyName: frontDoor.outputs.wafPolicyName
  }
}

output resourceGroupName string = rg.name
output containerAppUrl string = 'https://${containerApp.outputs.fqdn}'
output frontDoorEndpoint string = frontDoor.outputs.endpointHostName
output cosmosAccountName string = cosmos.outputs.accountName
output serviceBusNamespaceName string = serviceBus.outputs.namespaceName
output keyVaultName string = keyVault.outputs.vaultName
output applicationInsightsName string = monitoring.outputs.applicationInsightsName
