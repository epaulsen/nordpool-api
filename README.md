# nordpool-api

A .NET minimal API web service that caches Nordpool data and provides current electricity prices via REST endpoints.

## Features

- 🔄 Background service that fetches electricity prices from Nordpool API
  - Fetches today's prices on startup
  - Fetches tomorrow's prices after 3 PM Norwegian time
  - Automatically retries on HTTP 204 (data not available yet)
  - Cleans up old prices at midnight Norwegian time
- 📊 REST API endpoints for fetching electricity prices
- 🐳 Docker containerization support
- ☁️ GitHub Actions workflow for automated deployment to Azure Container Apps

## Endpoints

### Get Current Electricity Price
```
GET /api/prices/current?includeVAT=false
```
Returns the current electricity price value for the current hour.

**Query Parameters:**
- `includeVAT` (optional, default: `false`): When set to `true`, returns the price multiplied by 1.25 to include 25% VAT.

**Response:**
```json
1.7084
```

**Example with VAT:**
```
GET /api/prices/current?includeVAT=true
```

**Response:**
```json
2.1355
```

### Get All Prices
```
GET /api/prices
```
Returns all electricity prices available in the cache (today's prices, and tomorrow's if after 3 PM Norwegian time).

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

The service automatically schedules fetching based on Norwegian time (CET/CEST):
- **Startup**: Fetches today's prices (and tomorrow's if after 3 PM)
- **3 PM daily**: Fetches next day's prices (with 15-minute retry on HTTP 204)
- **Midnight daily**: Removes previous day's prices

No configuration needed - the service handles scheduling automatically.

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

## Data Parser

The `NordpoolDataParser` service parses Nordpool JSON data and extracts electricity prices from the `multiAreaEntries` section. The parser automatically converts prices from MWh to kWh.

### Usage Example

```csharp
using NordpoolApi.Services;

var parser = new NordpoolDataParser();
var jsonData = File.ReadAllText("nordpool-data.json");
var prices = parser.ParsePrices(jsonData);

foreach (var price in prices)
{
    Console.WriteLine($"{price.Area}: {price.Price} {price.Currency}/kWh at {price.Start}");
}
```

### Input Data Format

The parser expects JSON data in the Nordpool API format:

```json
{
  "deliveryDateCET": "2025-10-17",
  "currency": "NOK",
  "multiAreaEntries": [
    {
      "deliveryStart": "2025-10-16T22:00:00Z",
      "deliveryEnd": "2025-10-16T22:15:00Z",
      "entryPerArea": {
        "NO1": 726.47,
        "NO2": 806.87,
        "NO3": 328.03
      }
    }
  ]
}
```

### Features

- ✅ Parses `multiAreaEntries` from Nordpool JSON data
- ✅ Converts prices from MWh to kWh (divides by 1000)
- ✅ Extracts prices for all delivery areas (NO1, NO2, NO3, NO4, NO5, etc.)
- ✅ Preserves time information (delivery start and end)
- ✅ Returns strongly-typed `ElectricityPrice` objects

## Project Structure

```
nordpool-api/
├── .github/
│   └── workflows/
│       └── azure-container-apps.yml    # GitHub Actions deployment workflow
├── src/
│   └── NordpoolApi/
│       ├── Models/
│       │   ├── ElectricityPrice.cs     # Price data model
│       │   └── NordpoolData.cs         # Nordpool JSON data model
│       ├── Services/
│       │   ├── IPriceService.cs        # Price service interface
│       │   ├── PriceService.cs         # Price service implementation
│       │   ├── INordpoolApiClient.cs   # Nordpool API client interface
│       │   ├── NordpoolApiClient.cs    # Nordpool API client implementation
│       │   ├── NordpoolPollingService.cs # Background polling service with scheduling
│       │   └── NordpoolDataParser.cs   # Nordpool data parser
│       ├── Program.cs                   # Application entry point & API endpoints
│       ├── appsettings.json            # Application configuration
│       └── NordpoolApi.csproj          # Project file
├── tests/
│   └── NordpoolApi.Tests/
│       ├── ApiEndpointsTests.cs        # API endpoint tests
│       ├── NordpoolDataParserTests.cs  # Parser tests
│       └── testdata/
│           └── sampledata.json         # Sample Nordpool data
├── Dockerfile                           # Docker build instructions
└── README.md                            # This file
```

## Development Notes

### Current Implementation
- ✅ **Nordpool API integration** - Fetches real electricity prices from Nordpool API
- ✅ **Scheduled fetching** - Automatically fetches prices at 3 PM and cleans up at midnight (Norwegian time)
- ✅ **Retry logic** - Retries every 15 minutes when data is not yet available (HTTP 204)
- ✅ **Data parser** - Parses Nordpool JSON data with automatic MWh to kWh conversion
- ✅ **Timezone handling** - Uses Norwegian time (CET/CEST) for scheduling
- Prices are cached in memory using a thread-safe `ConcurrentDictionary`

### Data Source
The service fetches data from:
```
https://dataportal-api.nordpoolgroup.com/api/DayAheadPrices
```
Parameters:
- `date`: Date to fetch (YYYY-MM-DD format)
- `market`: DayAhead
- `deliveryArea`: NO1,NO2,NO3,NO4,NO5
- `currency`: NOK

### TODO
- Add support for multiple currencies
- Add price history and forecasting
- Add caching layer (Redis/Azure Cache)
- Add authentication/authorization if needed
- Add rate limiting

## License

This project is open source and available under the MIT License.

