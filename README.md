# nordpool-api

A .NET minimal API web service that caches Nordpool data and provides current electricity prices via REST endpoints.

## Features

- 🔄 Background service that polls Nordpool data at regular intervals (configurable, default: hourly)
- 📊 REST API endpoints for fetching electricity prices
- 🐳 Docker containerization support
- ☁️ GitHub Actions workflow for automated deployment to Azure Container Apps

## Endpoints

### Get Current Electricity Price
```
GET /api/prices/current
```
Returns the current electricity price for the current hour.

**Response:**
```json
{
  "start": "2025-10-16T15:00:00Z",
  "end": "2025-10-16T16:00:00Z",
  "price": 1.7084,
  "currency": "NOK",
  "area": "NO1"
}
```

### Get All Prices
```
GET /api/prices
```
Returns all electricity prices for today (24 hours).

**Response:**
```json
[
  {
    "start": "2025-10-16T00:00:00Z",
    "end": "2025-10-16T01:00:00Z",
    "price": 0.8542,
    "currency": "NOK",
    "area": "NO1"
  },
  ...
]
```

### Health Check
```
GET /health
```
Returns the health status of the application.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-10-16T15:48:48.2528823Z"
}
```

## Running Locally

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run the Application
```bash
cd src/NordpoolApi
dotnet run
```

The application will start on `http://localhost:5039` (or check console output for the actual port).

### Configuration

Edit `appsettings.json` to configure the polling interval:

```json
{
  "NordpoolPolling": {
    "IntervalMinutes": 60
  }
}
```

## Running with Docker

### Build the Docker Image
```bash
docker build -t nordpool-api .
```

### Run the Container
```bash
docker run -p 8080:8080 nordpool-api
```

The application will be available at `http://localhost:8080`.

## Deployment to Azure Container Apps

This repository includes a GitHub Actions workflow for automatic deployment to Azure Container Apps.

### Required GitHub Secrets

Before the workflow can run successfully, you need to configure the following secrets in your GitHub repository (Settings → Secrets and variables → Actions):

1. **`AZURE_CREDENTIALS`** - Azure service principal credentials
   ```json
   {
     "clientId": "<client-id>",
     "clientSecret": "<client-secret>",
     "subscriptionId": "<subscription-id>",
     "tenantId": "<tenant-id>"
   }
   ```

2. **`AZURE_CONTAINER_REGISTRY`** - Azure Container Registry name (e.g., `myregistry.azurecr.io`)

3. **`AZURE_REGISTRY_USERNAME`** - Azure Container Registry username

4. **`AZURE_REGISTRY_PASSWORD`** - Azure Container Registry password

5. **`AZURE_CONTAINER_APP_NAME`** - Name of your Azure Container App

6. **`AZURE_RESOURCE_GROUP`** - Azure resource group name

7. **`AZURE_CONTAINER_ENVIRONMENT`** - Azure Container Apps environment name

### Setting up Azure Resources

#### 1. Create an Azure Container Registry
```bash
az acr create --resource-group <resource-group> \
  --name <registry-name> --sku Basic
```

#### 2. Create an Azure Container Apps Environment
```bash
az containerapp env create \
  --name <environment-name> \
  --resource-group <resource-group> \
  --location <location>
```

#### 3. Create the Container App
```bash
az containerapp create \
  --name <app-name> \
  --resource-group <resource-group> \
  --environment <environment-name> \
  --image mcr.microsoft.com/dotnet/samples:aspnetapp \
  --target-port 8080 \
  --ingress external
```

#### 4. Create a Service Principal
```bash
az ad sp create-for-rbac --name "nordpool-api-deploy" \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/<resource-group> \
  --sdk-auth
```

Copy the output and use it for the `AZURE_CREDENTIALS` secret.

### Triggering Deployment

The workflow is triggered:
- Automatically on push to the `main` branch
- Manually via the "Actions" tab using "Run workflow"

## Project Structure

```
nordpool-api/
├── .github/
│   └── workflows/
│       └── azure-container-apps.yml    # GitHub Actions deployment workflow
├── src/
│   └── NordpoolApi/
│       ├── Models/
│       │   └── ElectricityPrice.cs     # Price data model
│       ├── Services/
│       │   ├── IPriceService.cs        # Price service interface
│       │   ├── PriceService.cs         # Price service implementation
│       │   └── NordpoolPollingService.cs # Background polling service
│       ├── Program.cs                   # Application entry point & API endpoints
│       ├── appsettings.json            # Application configuration
│       └── NordpoolApi.csproj          # Project file
├── Dockerfile                           # Docker build instructions
└── README.md                            # This file
```

## Development Notes

### Current Implementation
- The polling service currently generates **mock data** for demonstration purposes
- The service polls at a configurable interval (default: 60 minutes)
- Prices are cached in memory using a thread-safe `ConcurrentBag`

### TODO
- Replace mock data with actual Nordpool API integration
- Add support for multiple price areas (currently hardcoded to NO1)
- Add support for multiple currencies
- Add price history and forecasting
- Add caching layer (Redis/Azure Cache)
- Add authentication/authorization if needed
- Add rate limiting
- Add comprehensive unit and integration tests

## License

This project is open source and available under the MIT License.

