using System.Net;

namespace NordpoolApi.Services;

public class NordpoolApiClient : INordpoolApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NordpoolApiClient> _logger;
    private const string BaseUrl = "https://dataportal-api.nordpoolgroup.com/api/DayAheadPrices";
    private const string Market = "DayAhead";
    private const string DeliveryAreas = "NO1,NO2,NO3,NO4,NO5";
    private const string Currency = "NOK";

    public NordpoolApiClient(HttpClient httpClient, ILogger<NordpoolApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> FetchPriceDataAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?date={date:yyyy-MM-dd}&market={Market}&deliveryArea={DeliveryAreas}&currency={Currency}";
        
        _logger.LogInformation("Fetching Nordpool prices for {Date} from {Url}", date, url);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                _logger.LogInformation("No data available yet for {Date} (HTTP 204)", date);
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Successfully fetched data for {Date}", date);
            
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while fetching prices for {Date}", date);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching prices for {Date}", date);
            throw;
        }
    }
}
