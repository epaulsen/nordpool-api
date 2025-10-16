# Azure Deployment Setup Guide

This guide will help you set up the required Azure resources and GitHub secrets for automated deployment of the Nordpool API to Azure Container Apps.

## Prerequisites

- Azure CLI installed (`az cli`)
- An Azure subscription
- GitHub repository with appropriate permissions to add secrets

## Step 1: Create Azure Resources

### 1.1 Login to Azure
```bash
az login
```

### 1.2 Set Variables
```bash
RESOURCE_GROUP="nordpool-api-rg"
LOCATION="northeurope"  # or your preferred location
ACR_NAME="nordpoolacr"  # must be globally unique
CONTAINER_APP_ENV="nordpool-env"
CONTAINER_APP_NAME="nordpool-api"
```

### 1.3 Create Resource Group
```bash
az group create --name $RESOURCE_GROUP --location $LOCATION
```

### 1.4 Create Azure Container Registry
```bash
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Basic \
  --admin-enabled true
```

Get the registry credentials:
```bash
az acr credential show --name $ACR_NAME --resource-group $RESOURCE_GROUP
```

Save the username and password for GitHub secrets.

### 1.5 Create Container Apps Environment
```bash
az containerapp env create \
  --name $CONTAINER_APP_ENV \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION
```

### 1.6 Create Container App
```bash
az containerapp create \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINER_APP_ENV \
  --image mcr.microsoft.com/dotnet/samples:aspnetapp \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 3 \
  --cpu 0.5 \
  --memory 1.0Gi
```

## Step 2: Create Service Principal

Create a service principal for GitHub Actions to authenticate with Azure:

```bash
az ad sp create-for-rbac \
  --name "nordpool-api-github-deploy" \
  --role contributor \
  --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth
```

Save the entire JSON output for the `AZURE_CREDENTIALS` secret.

## Step 3: Configure GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions → New repository secret

Add the following secrets:

### AZURE_CREDENTIALS
The JSON output from the service principal creation (Step 2)
```json
{
  "clientId": "...",
  "clientSecret": "...",
  "subscriptionId": "...",
  "tenantId": "..."
}
```

### AZURE_CONTAINER_REGISTRY
Format: `<acr-name>.azurecr.io`
Example: `nordpoolacr.azurecr.io`

### AZURE_REGISTRY_USERNAME
The username from Step 1.4 (ACR credentials)

### AZURE_REGISTRY_PASSWORD
The password from Step 1.4 (ACR credentials)

### AZURE_CONTAINER_APP_NAME
The name of your container app
Example: `nordpool-api`

### AZURE_RESOURCE_GROUP
The name of your resource group
Example: `nordpool-api-rg`

### AZURE_CONTAINER_ENVIRONMENT
The name of your container apps environment
Example: `nordpool-env`

## Step 4: Verify Deployment

Once you push to the `main` branch or manually trigger the workflow:

1. Go to the Actions tab in your GitHub repository
2. Watch the workflow run
3. Once complete, get the application URL:

```bash
az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query properties.configuration.ingress.fqdn \
  -o tsv
```

4. Test the application:
```bash
APP_URL=$(az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query properties.configuration.ingress.fqdn \
  -o tsv)

curl https://$APP_URL/health
curl https://$APP_URL/api/prices/current
curl https://$APP_URL/api/prices
```

## Step 5: Configure Application Settings (Optional)

To change the polling interval in production:

```bash
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars "NordpoolPolling__IntervalMinutes=30"
```

## Monitoring and Logs

View logs:
```bash
az containerapp logs show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --follow
```

View metrics in Azure Portal:
1. Navigate to your Container App
2. Click on "Metrics" in the left menu
3. Select metrics like CPU, Memory, HTTP requests, etc.

## Cleanup

To delete all resources:
```bash
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

## Troubleshooting

### Workflow fails at "Build and push Docker image"
- Verify ACR credentials in GitHub secrets
- Check that admin user is enabled on ACR
- Verify the registry name format is correct

### Workflow fails at "Deploy to Azure Container Apps"
- Verify service principal has correct permissions
- Check that all secret names match exactly
- Ensure the container app and environment exist

### Application returns 502/503 errors
- Check container logs for startup errors
- Verify the target port (8080) matches the application configuration
- Check resource limits (CPU/Memory)

## Additional Resources

- [Azure Container Apps Documentation](https://docs.microsoft.com/azure/container-apps/)
- [Azure Container Registry Documentation](https://docs.microsoft.com/azure/container-registry/)
- [GitHub Actions for Azure](https://github.com/Azure/actions)
