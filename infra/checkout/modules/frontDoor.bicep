param env string
param location string = 'global'
@minLength(2)
param namePrefix string
param tags object
param originHostName string
param allowedCountryCodes array = []

var workload = 'checkout'
var resourceBase = toLower('${namePrefix}-${env}-${workload}')
var profileName = take('${resourceBase}-afd', 90)
var endpointName = take(replace('${resourceBase}-endpoint', '-', ''), 46)
var geoRules = length(allowedCountryCodes) == 0 ? [] : [
  {
    name: 'GeoFilterCheckout'
    enabledState: 'Enabled'
    priority: 30
    ruleType: 'MatchRule'
    action: 'Block'
    matchConditions: [
      {
        matchVariable: 'RemoteAddr'
        operator: 'GeoMatch'
        negateCondition: true
        matchValue: allowedCountryCodes
      }
    ]
  }
]

resource waf 'Microsoft.Network/frontDoorWebApplicationFirewallPolicies@2024-02-01' = {
  name: '${resourceBase}-waf'
  location: location
  tags: tags
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    policySettings: {
      enabledState: 'Enabled'
      mode: 'Prevention'
      requestBodyCheck: 'Enabled'
    }
    managedRules: {
      managedRuleSets: [
        {
          ruleSetType: 'Microsoft_DefaultRuleSet'
          ruleSetVersion: '2.1'
          ruleSetAction: 'Block'
        }
        {
          ruleSetType: 'Microsoft_BotManagerRuleSet'
          ruleSetVersion: '1.0'
          ruleSetAction: 'Block'
        }
      ]
    }
    customRules: {
      rules: concat([
        {
          name: 'RateLimitCheckoutApi'
          enabledState: 'Enabled'
          priority: 10
          ruleType: 'RateLimitRule'
          rateLimitDurationInMinutes: 1
          rateLimitThreshold: 100
          action: 'Block'
          matchConditions: [
            {
              matchVariable: 'RequestUri'
              operator: 'Contains'
              negateCondition: false
              matchValue: [
                '/api/checkout/'
              ]
              transforms: [
                'Lowercase'
              ]
            }
          ]
        }
        {
          name: 'BlockKnownBadBots'
          enabledState: 'Enabled'
          priority: 20
          ruleType: 'MatchRule'
          action: 'Block'
          matchConditions: [
            {
              matchVariable: 'RequestHeader'
              selector: 'User-Agent'
              operator: 'Regex'
              negateCondition: false
              matchValue: [
                '(?i)(badbot|evilbot|scrapy|masscan|sqlmap|nikto)'
              ]
            }
          ]
        }
      ], geoRules)
    }
  }
}

resource profile 'Microsoft.Cdn/profiles@2024-09-01' = {
  name: profileName
  location: location
  tags: tags
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource endpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-09-01' = {
  parent: profile
  name: endpointName
  location: location
  tags: tags
  properties: {
    enabledState: 'Enabled'
  }
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2024-09-01' = {
  parent: profile
  name: 'checkout-origin-group'
  properties: {
    sessionAffinityState: 'Disabled'
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/health'
      probeProtocol: 'Https'
      probeRequestType: 'GET'
      probeIntervalInSeconds: 100
    }
  }
}

resource origin 'Microsoft.Cdn/profiles/originGroups/origins@2024-09-01' = {
  parent: originGroup
  name: 'checkout-api'
  properties: {
    hostName: originHostName
    originHostHeader: originHostName
    httpPort: 80
    httpsPort: 443
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-09-01' = {
  parent: endpoint
  name: 'checkout-route'
  properties: {
    customDomains: []
    originGroup: {
      id: originGroup.id
    }
    supportedProtocols: [
      'Https'
    ]
    patternsToMatch: [
      '/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    httpsRedirect: 'Enabled'
    enabledState: 'Enabled'
  }
}

resource securityPolicy 'Microsoft.Cdn/profiles/securityPolicies@2024-09-01' = {
  parent: profile
  name: 'checkout-waf-policy'
  properties: {
    parameters: {
      type: 'WebApplicationFirewall'
      wafPolicy: {
        id: waf.id
      }
      associations: [
        {
          domains: [
            {
              id: endpoint.id
            }
          ]
          patternsToMatch: [
            '/*'
          ]
        }
      ]
    }
  }
}

output profileName string = profile.name
output profileId string = profile.id
output endpointHostName string = endpoint.properties.hostName
output wafPolicyName string = waf.name
output wafPolicyId string = waf.id
