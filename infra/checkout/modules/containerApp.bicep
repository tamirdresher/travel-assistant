param env string
param location string
@minLength(2)
param namePrefix string
param tags object
param acaSubnetId string
param containerImage string
param minReplicas int
param maxReplicas int
param paymentApiKeySecretUri string
param webhookSecretUri string

var workload = 'checkout'
var resourceBase = toLower('${namePrefix}-${env}-${workload}')
var appName = '${resourceBase}-api'

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${resourceBase}-aca-env'
  location: location
  tags: tags
  properties: {
    vnetConfiguration: {
      infrastructureSubnetId: acaSubnetId
      internal: false
    }
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      secrets: [
        {
          name: 'payment-provider-api-key'
          identity: 'system'
          keyVaultUrl: paymentApiKeySecretUri
        }
        {
          name: 'payment-provider-webhook-secret'
          identity: 'system'
          keyVaultUrl: webhookSecretUri
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'checkout-api'
          image: containerImage
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'CHECKOUT_ENVIRONMENT'
              value: env
            }
            {
              name: 'PAYMENT_PROVIDER_API_KEY'
              secretRef: 'payment-provider-api-key'
            }
            {
              name: 'PAYMENT_PROVIDER_WEBHOOK_SECRET'
              secretRef: 'payment-provider-webhook-secret'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output name string = app.name
output environmentName string = environment.name
output principalId string = app.identity.principalId
output fqdn string = app.properties.configuration.ingress.fqdn
