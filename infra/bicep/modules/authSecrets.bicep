@description('Key Vault name (must already exist)')
param keyVaultName string

@description('Refresh token signing key (HMAC). Generate with: openssl rand -base64 64')
@secure()
param refreshTokenSigningKey string

@description('Standard refresh token sliding TTL in seconds. Per sec-hard RM-005: 8 hours.')
param refreshTokenTtlSeconds int = 28800

@description('Long-lived ("remember me") refresh token sliding TTL in seconds. Per RM-005: 30 days.')
param refreshTokenLongTtlSeconds int = 2592000

@description('Absolute cap for any refresh token family (remember-me or not). Per RM-005: 90 days. After this the user MUST re-authenticate regardless of sliding renewals.')
param refreshTokenAbsoluteCapSeconds int = 7776000

@description('Auth cookie name. Per RM-005 the refresh token cookie is "ta_rt".')
param authCookieName string = 'ta_rt'

@description('Auth cookie path. Per RM-005 the cookie scope is restricted to /api/auth.')
param authCookiePath string = '/api/auth'

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

// Absolute cap — hard upper bound on token family lifetime even with sliding renewals (RM-005).
resource absoluteCapSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--RefreshToken--AbsoluteCapSeconds'
  properties: {
    value: string(refreshTokenAbsoluteCapSeconds)
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Cookie name — locked to ta_rt by RM-005. Stored so the API reads it from KV not appsettings.
resource cookieNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--Cookie--Name'
  properties: {
    value: authCookieName
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Cookie path — restricted to /api/auth so the cookie is never sent to static assets or the Next.js app router.
resource cookiePathSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: kv
  name: 'Auth--Cookie--Path'
  properties: {
    value: authCookiePath
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

output signingKeySecretUri string = signingKeySecret.properties.secretUri
output ttlSecretUri string = ttlSecret.properties.secretUri
output longTtlSecretUri string = longTtlSecret.properties.secretUri
output absoluteCapSecretUri string = absoluteCapSecret.properties.secretUri
output cookieDomainSecretUri string = cookieDomainSecret.properties.secretUri
output cookieSameSiteSecretUri string = cookieSameSiteSecret.properties.secretUri
output cookieNameSecretUri string = cookieNameSecret.properties.secretUri
output cookiePathSecretUri string = cookiePathSecret.properties.secretUri
