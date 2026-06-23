@description('Key Vault name (must already exist)')
param keyVaultName string

@description('Refresh token signing key (HMAC). Generate with: openssl rand -base64 64')
@secure()
param refreshTokenSigningKey string

@description('Standard refresh token TTL in seconds. Default 7 days.')
param refreshTokenTtlSeconds int = 604800

@description('Long-lived ("remember me") refresh token TTL in seconds. Default 30 days.')
param refreshTokenLongTtlSeconds int = 2592000

@description('Auth cookie domain for this environment (e.g., .dev.travel-assistant.example.com)')
param authCookieDomain string

@description('Auth cookie SameSite policy. Lax is correct for first-party login flow.')
@allowed([
  'Lax'
  'Strict'
  'None'
])
param authCookieSameSite string = 'Lax'

resource kv 'Microsoft.KeyVault/vaults@2023-02-01' existing = {
  name: keyVaultName
}

// Signing key — secret, rotated independently of TTL config.
resource signingKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--RefreshToken--SigningKey'
  properties: {
    value: refreshTokenSigningKey
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Standard refresh token TTL (non-remember-me)
resource ttlSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--RefreshToken--TtlSeconds'
  properties: {
    value: string(refreshTokenTtlSeconds)
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Long-lived refresh token TTL (remember-me checked)
resource longTtlSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--RefreshToken--LongTtlSeconds'
  properties: {
    value: string(refreshTokenLongTtlSeconds)
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Cookie domain — per-environment so dev/preview/prod can each set their own apex.
resource cookieDomainSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--Cookie--Domain'
  properties: {
    value: authCookieDomain
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

resource cookieSameSiteSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--Cookie--SameSite'
  properties: {
    value: authCookieSameSite
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

output signingKeySecretUri string = signingKeySecret.properties.secretUri
output ttlSecretUri string = ttlSecret.properties.secretUri
output longTtlSecretUri string = longTtlSecret.properties.secretUri
output cookieDomainSecretUri string = cookieDomainSecret.properties.secretUri
output cookieSameSiteSecretUri string = cookieSameSiteSecret.properties.secretUri
