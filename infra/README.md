# Travel Assistant — Azure Infrastructure

This directory contains Bicep templates for provisioning all Azure resources for the Travel Assistant MVP.

## Architecture

The infrastructure consists of:
- **Azure Container Apps** — .NET API backend (consumption tier, scale-to-zero)
- **Azure Static Web Apps** — Next.js frontend (Free tier)
- **Azure Cosmos DB** — NoSQL database for conversation history (serverless)
- **Azure Key Vault** — Secrets management (Standard tier)
- **Application Insights + Log Analytics** — Observability (pay-as-you-go)
- **Entra External ID** — User authentication (manual setup, see module README)

**Total estimated cost (dev environment, low traffic):** ~$5-10/month

## Prerequisites

1. **Azure CLI** version 2.50.0 or later
   ```bash
   az --version
   ```
   Install/upgrade: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli

2. **Azure subscription** with Owner or Contributor role

3. **Bicep CLI** (bundled with Azure CLI 2.20+)
   ```bash
   az bicep version
   ```

4. **Logged in to Azure**
   ```bash
   az login
   az account show  # Verify correct subscription
   az account set --subscription "<subscription-id-or-name>"  # If needed
   ```

## Deployment

### 1. Create Resource Group

```bash
# Dev environment
az group create \
  --name rg-travelassist-dev \
  --location eastus

# Staging environment (optional)
az group create \
  --name rg-travelassist-staging \
  --location eastus

# Production environment (optional)
az group create \
  --name rg-travelassist-prod \
  --location eastus
```

### 2. Validate Bicep Templates

```bash
# Validate main.bicep
az bicep build --file infra/bicep/main.bicep

# Validate with parameters
az deployment group validate \
  --resource-group rg-travelassist-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/dev.bicepparam
```

### 3. Deploy Infrastructure

```bash
# Deploy dev environment
az deployment group create \
  --resource-group rg-travelassist-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/dev.bicepparam \
  --name "infra-$(date +%Y%m%d-%H%M%S)"

# Or use what-if to preview changes
az deployment group what-if \
  --resource-group rg-travelassist-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/dev.bicepparam
```

### 4. Capture Outputs

After deployment, capture important outputs:

```bash
# Get all outputs
az deployment group show \
  --resource-group rg-travelassist-dev \
  --name <deployment-name> \
  --query properties.outputs

# Get specific values
az deployment group show \
  --resource-group rg-travelassist-dev \
  --name <deployment-name> \
  --query properties.outputs.apiAppFqdn.value -o tsv

az deployment group show \
  --resource-group rg-travelassist-dev \
  --name <deployment-name> \
  --query properties.outputs.staticWebAppDefaultHostname.value -o tsv
```

## Post-Deployment Configuration

### 1. Configure Entra External ID
See `modules/entraExternalId.bicep` (README) for manual setup steps.

### 2. Store Secrets in Key Vault

```bash
KV_NAME=$(az deployment group show \
  --resource-group rg-travelassist-dev \
  --name <deployment-name> \
  --query properties.outputs.keyVaultName.value -o tsv)

# Amadeus API credentials (from https://developers.amadeus.com/)
az keyvault secret set --vault-name $KV_NAME --name "AmadeusApiKey" --value "<your-api-key>"
az keyvault secret set --vault-name $KV_NAME --name "AmadeusApiSecret" --value "<your-api-secret>"

# Azure OpenAI credentials
az keyvault secret set --vault-name $KV_NAME --name "AzureOpenAIEndpoint" --value "<your-endpoint>"
az keyvault secret set --vault-name $KV_NAME --name "AzureOpenAIKey" --value "<your-key>"

# Entra External ID credentials (after manual setup)
az keyvault secret set --vault-name $KV_NAME --name "EntraClientId" --value "<client-id>"
az keyvault secret set --vault-name $KV_NAME --name "EntraClientSecret" --value "<client-secret>"
az keyvault secret set --vault-name $KV_NAME --name "EntraTenantId" --value "<tenant-id>"
```

### 3. Configure Static Web App (GitHub Integration)

Option A: Azure Portal
1. Navigate to the Static Web App resource
2. Click **Manage deployment token** → Copy token
3. Add token to GitHub repo secrets as `AZURE_STATIC_WEB_APPS_API_TOKEN`

Option B: Azure CLI
```bash
SWA_NAME=$(az deployment group show \
  --resource-group rg-travelassist-dev \
  --name <deployment-name> \
  --query properties.outputs.staticWebAppName.value -o tsv)

az staticwebapp secrets list \
  --name $SWA_NAME \
  --query properties.apiKey -o tsv
```

## Resource Naming Convention

All resources follow the pattern: `<resourcePrefix>-<environmentName>-<resourceType>`

Examples (dev environment, prefix `travelassist`):
- Container Apps Env: `travelassist-dev-cae`
- API Container App: `travelassist-dev-cae-api`
- Static Web App: `travelassist-dev-swa`
- Cosmos DB: `travelassist-dev-cosmos`
- Key Vault: `travelassist-dev-kv`
- App Insights: `travelassist-dev-ai`
- Log Workspace: `travelassist-dev-logs`

## Cost Optimization

All resources use cost-optimized tiers for MVP:

| Resource | Tier/Config | Est. Cost (dev) |
|---|---|---|
| Container Apps | Consumption (0.25 vCPU, 0.5 GB, scale-to-zero) | ~$2-5/month (low traffic) |
| Static Web Apps | Free (100 GB bandwidth) | $0 |
| Cosmos DB | Serverless (pay-per-request) | ~$1-3/month (<1M requests) |
| Key Vault | Standard | ~$0.03/month (10k ops) |
| Log Analytics | Pay-as-you-go (30-day retention) | ~$2-3/month (<5 GB) |
| App Insights | Bundled with Log Analytics | Included |
| Entra External ID | Free tier (<50k MAU) | $0 |

**Total: ~$5-10/month for dev environment**

Production will scale with traffic but starts at similar baseline.

## Updating Infrastructure

To update existing resources:

1. Modify Bicep templates or parameters
2. Run `az bicep build` to validate
3. Run `az deployment group what-if` to preview changes
4. Run `az deployment group create` to apply changes

Bicep is **idempotent** — safe to re-run without duplicating resources.

## Cleanup

To delete all resources:

```bash
# Delete resource group (deletes all resources)
az group delete --name rg-travelassist-dev --yes --no-wait
```

## Troubleshooting

### "Resource name already exists"
Key Vault names are globally unique. If deployment fails, try a different `resourcePrefix` in parameters.

### "Insufficient permissions"
Ensure your Azure account has **Contributor** or **Owner** role on the subscription/resource group.

### Bicep validation errors
```bash
# Install/update Bicep CLI
az bicep upgrade

# Check Bicep version
az bicep version  # Should be 0.15.0 or later
```

## References

- [Azure Bicep Documentation](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Container Apps Pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)
- [Static Web Apps Pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/static/)
- [Cosmos DB Serverless Pricing](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)
