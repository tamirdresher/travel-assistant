# Entra External ID Configuration

**Note:** Microsoft Entra External ID tenant configuration is not fully automatable via Bicep at this time. This module provides guidance for manual setup.

## Prerequisites
- Azure subscription with permissions to create Azure AD B2C / Entra External ID tenants
- Azure CLI or Azure Portal access

## Manual Setup Steps

### 1. Create Entra External ID Tenant (via Azure Portal)
1. Navigate to **Azure Portal** → **Create a resource** → Search for "Azure Active Directory B2C" or "External ID"
2. Click **Create a new Azure AD B2C Tenant** or **Create External ID**
3. Configure:
   - **Organization name:** Travel Assistant
   - **Initial domain name:** `travelassistant` (results in `travelassistant.onmicrosoft.com`)
   - **Country/Region:** Select your primary region
   - **Subscription:** Your Azure subscription
   - **Resource group:** Same as your other resources (e.g., `rg-travelassist-dev`)
4. Click **Create** (takes 1-2 minutes)

### 2. Configure User Flows (Sign-up and Sign-in)
1. In the new tenant, navigate to **Azure AD B2C** → **User flows**
2. Click **New user flow** → **Sign up and sign in**
3. Select **Recommended** version
4. Configure:
   - **Name:** `signupsignin`
   - **Identity providers:** 
     - ✅ Email signup
     - (Optional for post-MVP: Microsoft, Google, Facebook)
   - **User attributes and token claims:**
     - Collect: Email Address, Given Name, Surname
     - Return: Email Addresses, Given Name, Surname, User's Object ID
5. Click **Create**

### 3. Register Application (for API authentication)
1. Navigate to **App registrations** → **New registration**
2. Configure:
   - **Name:** `travel-assistant-api`
   - **Supported account types:** Accounts in this organizational directory only
   - **Redirect URI:** Leave blank for now (will configure after Container App deployment)
3. Click **Register**
4. Note down:
   - **Application (client) ID**
   - **Directory (tenant) ID**
5. Navigate to **Certificates & secrets** → **New client secret**
   - **Description:** `api-client-secret`
   - **Expires:** 24 months
6. Copy the secret value immediately (cannot be retrieved later)

### 4. Configure API Permissions
1. In the app registration, navigate to **API permissions**
2. Click **Add a permission** → **Microsoft Graph** → **Delegated permissions**
3. Select:
   - `User.Read` (for user profile)
   - `openid`
   - `profile`
   - `email`
4. Click **Add permissions**

### 5. Configure App Roles (Optional - for admin features post-MVP)
1. In the app registration, navigate to **App roles**
2. Click **Create app role**
3. Configure:
   - **Display name:** `Administrator`
   - **Allowed member types:** Users/Groups
   - **Value:** `Admin`
   - **Description:** `Administrators can access admin features`
4. Click **Apply**

## Store Credentials in Key Vault

After manual setup, store the following secrets in Azure Key Vault (created by `keyVault.bicep`):

```bash
# Replace with your actual values
az keyvault secret set --vault-name <key-vault-name> --name "EntraClientId" --value "<application-client-id>"
az keyvault secret set --vault-name <key-vault-name> --name "EntraClientSecret" --value "<client-secret-value>"
az keyvault secret set --vault-name <key-vault-name> --name "EntraTenantId" --value "<tenant-id>"
az keyvault secret set --vault-name <key-vault-name> --name "EntraInstance" --value "https://<tenant-name>.b2clogin.com"
az keyvault secret set --vault-name <key-vault-name> --name "EntraSignUpSignInPolicyId" --value "B2C_1_signupsignin"
```

## API Integration (for Peres)

The .NET API should use `Microsoft.Identity.Web` package to validate JWT tokens:

```csharp
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAdB2C");
```

`appsettings.json` (values from Key Vault):
```json
{
  "AzureAdB2C": {
    "Instance": "https://<tenant-name>.b2clogin.com",
    "ClientId": "<application-client-id>",
    "Domain": "<tenant-name>.onmicrosoft.com",
    "SignUpSignInPolicyId": "B2C_1_signupsignin"
  }
}
```

## Frontend Integration (for Lapid)

The Next.js frontend should use `@azure/msal-browser` or `@azure/msal-react`:

```typescript
import { PublicClientApplication } from '@azure/msal-browser';

const msalConfig = {
  auth: {
    clientId: process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID!,
    authority: `https://<tenant-name>.b2clogin.com/<tenant-name>.onmicrosoft.com/B2C_1_signupsignin`,
    knownAuthorities: ['<tenant-name>.b2clogin.com'],
    redirectUri: process.env.NEXT_PUBLIC_REDIRECT_URI || 'http://localhost:3000',
  },
};

const msalInstance = new PublicClientApplication(msalConfig);
```

## Cost Considerations

- **Free tier:** Up to 50,000 monthly active users (MAU) free
- **Additional MAU:** $0.00325 per MAU above 50,000
- **MFA:** Additional $0.03 per authentication (opt-in)

For MVP with < 1,000 users, cost is **$0/month**.

## References
- [Azure AD B2C Documentation](https://learn.microsoft.com/en-us/azure/active-directory-b2c/)
- [Entra External ID Overview](https://learn.microsoft.com/en-us/azure/active-directory/external-identities/)
