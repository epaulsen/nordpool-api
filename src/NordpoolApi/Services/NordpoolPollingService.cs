using NordpoolApi.Models;

namespace NordpoolApi.Services;

public class NordpoolPollingService : BackgroundService
{
    private readonly ILogger<NordpoolPollingService> _logger;
    private readonly PriceService _priceService;
    private readonly INordpoolApiClient _apiClient;
    private readonly NordpoolDataParser _dataParser;
    private static readonly TimeZoneInfo NorwegianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
    private const int DailyFetchHour = 15; // 3 PM
    private const int RetryDelayMinutes = 15;

    public NordpoolPollingService(
        ILogger<NordpoolPollingService> logger,
        PriceService priceService,
        INordpoolApiClient apiClient,
        NordpoolDataParser dataParser)
    {
        _logger = logger;
        _priceService = priceService;
        _apiClient = apiClient;
        _dataParser = dataParser;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Nordpool polling service started");

        try
        {
            // Fetch initial data on startup
            await FetchInitialPricesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching initial Nordpool prices");
        }

        // Schedule periodic tasks
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = GetNorwegianTime();
                
                // Check if we need to fetch tomorrow's prices (at 3 PM)
                var nextFetchTime = GetNext3PMTime(now);
                var timeUntilNextFetch = nextFetchTime - now;
                
                // Check if we need to clean old data (at midnight)
                var nextMidnight = GetNextMidnight(now);
                var timeUntilMidnight = nextMidnight - now;
                
                // Wait until the next event
                var nextEventTime = timeUntilNextFetch < timeUntilMidnight ? timeUntilNextFetch : timeUntilMidnight;
                
                _logger.LogInformation("Next fetch at {FetchTime}, next cleanup at {MidnightTime}", 
                    nextFetchTime, nextMidnight);
                
                if (nextEventTime > TimeSpan.Zero)
                {
                    await Task.Delay(nextEventTime, stoppingToken);
                }
                
                now = GetNorwegianTime();
                
                // Check which task to execute
                if (Math.Abs((now - nextFetchTime).TotalMinutes) < 1)
                {
                    await FetchTomorrowPricesWithRetryAsync(stoppingToken);
                }
                else if (Math.Abs((now - nextMidnight).TotalMinutes) < 1)
                {
                    CleanOldPrices();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in polling loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Nordpool polling service stopped");
    }

    private async Task FetchInitialPricesAsync(CancellationToken cancellationToken)
    {
        var now = GetNorwegianTime();
        var today = DateOnly.FromDateTime(now);
        
        _logger.LogInformation("Fetching initial prices for today: {Date}", today);
        
        // Always fetch today's prices
        await FetchAndStorePricesAsync(today, isInitialLoad: true, cancellationToken);
        
        // If it's after 3 PM, also fetch tomorrow's prices
        if (now.Hour >= DailyFetchHour)
        {
            var tomorrow = today.AddDays(1);
            _logger.LogInformation("Time is after 3 PM, also fetching tomorrow's prices: {Date}", tomorrow);
            await FetchAndStorePricesAsync(tomorrow, isInitialLoad: true, cancellationToken);
        }
    }

    private async Task FetchTomorrowPricesWithRetryAsync(CancellationToken cancellationToken)
    {
        var tomorrow = DateOnly.FromDateTime(GetNorwegianTime()).AddDays(1);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await FetchAndStorePricesAsync(tomorrow, isInitialLoad: false, cancellationToken);
            
            if (result)
            {
                // Successfully fetched data
                break;
            }
            
            // Data not available yet (HTTP 204), wait and retry
            _logger.LogInformation("Data not available yet, waiting {Minutes} minutes before retry", RetryDelayMinutes);
            await Task.Delay(TimeSpan.FromMinutes(RetryDelayMinutes), cancellationToken);
        }
    }

    private async Task<bool> FetchAndStorePricesAsync(DateOnly date, bool isInitialLoad, CancellationToken cancellationToken)
    {
        try
        {
            var jsonData = await _apiClient.FetchPriceDataAsync(date, cancellationToken);
            
            if (jsonData == null)
            {
                // No data available (HTTP 204)
                return false;
            }
            
            var prices = _dataParser.ParsePrices(jsonData);
            
            if (isInitialLoad)
            {
                _priceService.AddPrices(prices);
            }
            else
            {
                _priceService.AddPrices(prices);
            }
            
            _logger.LogInformation("Successfully fetched and stored {Count} prices for {Date}", 
                prices.Count(), date);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching prices for {Date}", date);
            throw;
        }
    }

    private void CleanOldPrices()
    {
        var now = GetNorwegianTime();
        var startOfToday = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var startOfTodayUtc = TimeZoneInfo.ConvertTimeToUtc(startOfToday, NorwegianTimeZone);
        
        _logger.LogInformation("Cleaning old prices before {Date}", startOfTodayUtc);
        _priceService.RemoveOldPrices(startOfTodayUtc);
    }

    private DateTime GetNorwegianTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NorwegianTimeZone);
    }

    private DateTime GetNext3PMTime(DateTime norwegianNow)
    {
        var today3PM = new DateTime(norwegianNow.Year, norwegianNow.Month, norwegianNow.Day, 
            DailyFetchHour, 0, 0, DateTimeKind.Unspecified);
        
        if (norwegianNow >= today3PM)
        {
            return today3PM.AddDays(1);
        }
        
        return today3PM;
    }

    private DateTime GetNextMidnight(DateTime norwegianNow)
    {
        var tomorrow = norwegianNow.Date.AddDays(1);
        return new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 0, 0, 0, DateTimeKind.Unspecified);
    }
}
