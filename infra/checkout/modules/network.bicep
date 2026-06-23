param env string
param location string
@minLength(2)
param namePrefix string
param tags object

var workload = 'checkout'
var resourceBase = toLower('${namePrefix}-${env}-${workload}')
var addressPrefix = env == 'prod' ? '10.42.0.0/16' : '10.41.0.0/16'

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: '${resourceBase}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: 'pe-subnet'
        properties: {
          addressPrefix: env == 'prod' ? '10.42.1.0/24' : '10.41.1.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
      {
        name: 'aca-subnet'
        properties: {
          addressPrefix: env == 'prod' ? '10.42.2.0/23' : '10.41.2.0/23'
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
    ]
  }
}

resource cosmosZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.documents.azure.com'
  location: 'global'
  tags: tags
}

resource keyVaultZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
  tags: tags
}

resource serviceBusZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.servicebus.windows.net'
  location: 'global'
  tags: tags
}

resource cosmosZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: cosmosZone
  name: '${resourceBase}-cosmos-link'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

resource keyVaultZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: keyVaultZone
  name: '${resourceBase}-kv-link'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

resource serviceBusZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: serviceBusZone
  name: '${resourceBase}-sb-link'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

output vnetId string = vnet.id
output privateEndpointSubnetId string = '${vnet.id}/subnets/pe-subnet'
output acaSubnetId string = '${vnet.id}/subnets/aca-subnet'
output cosmosPrivateDnsZoneId string = cosmosZone.id
output keyVaultPrivateDnsZoneId string = keyVaultZone.id
output serviceBusPrivateDnsZoneId string = serviceBusZone.id
