# Azure Deployment Setup Guide

This guide will help you set up the required Azure resources and GitHub secrets for automated deployment of the Nordpool API to Azure Web App for Containers.

## Prerequisites

- Azure CLI installed (`az cli`)
- An Azure subscription
- GitHub repository with appropriate permissions to add secrets

## Quick Setup with Bicep (Recommended)

The easiest way to set up all required resources is to use the provided Bicep template.

### 1. Login to Azure
```bash
az login
```

### 2. Set Variables
```bash
RESOURCE_GROUP="nordpool-api-rg"
LOCATION="northeurope"  # or your preferred location
ACR_NAME="nordpoolacr"  # must be globally unique
WEBAPP_NAME="nordpool-api"
APP_SERVICE_PLAN="nordpool-asp"
```

### 3. Create Service Principal
Create a service principal for GitHub Actions to authenticate with Azure:

```bash
# Create the resource group first
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create service principal with contributor role on the resource group
SP_OUTPUT=$(az ad sp create-for-rbac \
  --name "nordpool-api-github-deploy" \
  --role contributor \
  --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth)

echo "$SP_OUTPUT"
```

Save the entire JSON output for the `AZURE_CREDENTIALS` secret.

### 4. Get Service Principal Object ID
```bash
SP_OBJECT_ID=$(az ad sp list --display-name "nordpool-api-github-deploy" --query "[0].id" -o tsv)
echo "Service Principal Object ID: $SP_OBJECT_ID"
```

### 5. Deploy Resources Using Bicep
```bash
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infrastructure.bicep \
  --parameters \
    acrName=$ACR_NAME \
    appServicePlanName=$APP_SERVICE_PLAN \
    webAppName=$WEBAPP_NAME \
    servicePrincipalObjectId=$SP_OBJECT_ID \
    location=$LOCATION
```

### 6. Get Deployment Outputs
```bash
ACR_LOGIN_SERVER=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name infrastructure \
  --query properties.outputs.containerRegistryLoginServer.value -o tsv)

WEBAPP_URL=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name infrastructure \
  --query properties.outputs.webAppUrl.value -o tsv)

echo "ACR Login Server: $ACR_LOGIN_SERVER"
echo "Web App URL: $WEBAPP_URL"
```

## Manual Setup (Alternative)

If you prefer to set up resources manually instead of using Bicep:

### 1. Login to Azure
```bash
az login
```

### 2. Set Variables
```bash
RESOURCE_GROUP="nordpool-api-rg"
LOCATION="northeurope"  # or your preferred location
ACR_NAME="nordpoolacr"  # must be globally unique
WEBAPP_NAME="nordpool-api"
APP_SERVICE_PLAN="nordpool-asp"
```

### 3. Create Resource Group
```bash
az group create --name $RESOURCE_GROUP --location $LOCATION
```

### 4. Create Azure Container Registry
```bash
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Basic \
  --admin-enabled false
```

**Note:** Admin user is disabled because we use service principal authentication instead.

### 5. Create Service Principal
Create a service principal for GitHub Actions:

```bash
SP_OUTPUT=$(az ad sp create-for-rbac \
  --name "nordpool-api-github-deploy" \
  --role contributor \
  --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth)

echo "$SP_OUTPUT"
```

Save the entire JSON output for the `AZURE_CREDENTIALS` secret.

### 6. Grant Service Principal AcrPush Role
```bash
SP_OBJECT_ID=$(az ad sp list --display-name "nordpool-api-github-deploy" --query "[0].id" -o tsv)

az role assignment create \
  --assignee-object-id $SP_OBJECT_ID \
  --assignee-principal-type ServicePrincipal \
  --role AcrPush \
  --scope /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ContainerRegistry/registries/$ACR_NAME
```

### 7. Create App Service Plan
```bash
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --is-linux \
  --sku B1
```

### 8. Create Web App for Containers
```bash
az webapp create \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --name $WEBAPP_NAME \
  --deployment-container-image-name mcr.microsoft.com/dotnet/samples:aspnetapp
```

### 9. Configure Web App
```bash
# Enable system-assigned managed identity
az webapp identity assign \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME

# Get the managed identity principal ID
WEBAPP_PRINCIPAL_ID=$(az webapp identity show \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME \
  --query principalId -o tsv)

# Grant the Web App's managed identity AcrPull role
az role assignment create \
  --assignee-object-id $WEBAPP_PRINCIPAL_ID \
  --assignee-principal-type ServicePrincipal \
  --role AcrPull \
  --scope /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ContainerRegistry/registries/$ACR_NAME

# Configure container settings
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME \
  --settings \
    WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \
    DOCKER_REGISTRY_SERVER_URL=https://$ACR_NAME.azurecr.io \
    WEBSITES_PORT=8080

# Configure health check
az webapp config set \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME \
  --health-check-path /health \
  --ftps-state Disabled \
  --always-on true

# Configure container to use ACR with managed identity
az webapp config container set \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME \
  --docker-registry-server-url https://$ACR_NAME.azurecr.io
```

## Configure GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions → New repository secret

Add the following secrets:

### AZURE_CREDENTIALS
The JSON output from the service principal creation (Step 3 in Quick Setup or Step 5 in Manual Setup)
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

### AZURE_WEBAPP_NAME
The name of your Azure Web App
Example: `nordpool-api`

### AZURE_RESOURCE_GROUP
The name of your resource group
Example: `nordpool-api-rg`

## Verify Deployment

Once you push to the `main` branch or manually trigger the workflow:

1. Go to the Actions tab in your GitHub repository
2. Watch the workflow run
3. Once complete, test the application:

```bash
WEBAPP_URL=$(az webapp show \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query defaultHostName -o tsv)

curl https://$WEBAPP_URL/health
curl https://$WEBAPP_URL/api/prices/current
curl https://$WEBAPP_URL/api/prices
```

## Configure Application Settings (Optional)

To change the polling interval in production:

```bash
az webapp config appsettings set \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings "NordpoolPolling__IntervalMinutes=30"
```

## Monitoring and Logs

### View logs:
```bash
az webapp log tail \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP
```

### View metrics in Azure Portal:
1. Navigate to your Web App
2. Click on "Metrics" in the left menu
3. Select metrics like CPU, Memory, HTTP requests, etc.

### Enable Application Insights (Optional):
```bash
# Create Application Insights
az monitor app-insights component create \
  --app nordpool-insights \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app nordpool-insights \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)

# Configure Web App to use Application Insights
az webapp config appsettings set \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY
```

## Cleanup

To delete all resources:
```bash
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

## Troubleshooting

### Workflow fails at "Build and push Docker image"
- Verify service principal has AcrPush role on ACR
- Check that all secret names match exactly in GitHub
- Ensure the service principal credentials are valid

### Workflow fails at "Deploy to Azure Web App"
- Verify service principal has contributor role on resource group
- Check that AZURE_WEBAPP_NAME matches your actual web app name
- Ensure the web app exists and is in the correct resource group

### Application returns 502/503 errors after deployment
- Check container logs: `az webapp log tail --name $WEBAPP_NAME --resource-group $RESOURCE_GROUP`
- Verify the health check endpoint is responding: `/health`
- Check that WEBSITES_PORT is set to 8080
- Verify the managed identity has AcrPull role on ACR

### Container fails to pull from ACR
- Verify the Web App's managed identity has AcrPull role
- Check that DOCKER_REGISTRY_SERVER_URL is correctly set
- Ensure admin user is disabled and managed identity authentication is being used

## Additional Resources

- [Azure Web App for Containers Documentation](https://docs.microsoft.com/azure/app-service/containers/)
- [Azure Container Registry Documentation](https://docs.microsoft.com/azure/container-registry/)
- [GitHub Actions for Azure](https://github.com/Azure/actions)
- [Bicep Documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)
