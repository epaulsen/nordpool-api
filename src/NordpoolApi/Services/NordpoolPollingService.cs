using NordpoolApi.Models;

namespace NordpoolApi.Services;

public class NordpoolPollingService : BackgroundService
{
    private readonly ILogger<NordpoolPollingService> _logger;
    private readonly PriceService _priceService;
    private readonly TimeSpan _pollingInterval;

    public NordpoolPollingService(
        ILogger<NordpoolPollingService> logger,
        PriceService priceService,
        IConfiguration configuration)
    {
        _logger = logger;
        _priceService = priceService;
        
        var intervalMinutes = configuration.GetValue<int>("NordpoolPolling:IntervalMinutes", 60);
        _pollingInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Nordpool polling service started with interval: {Interval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndUpdatePricesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching Nordpool prices");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Nordpool polling service stopped");
    }

    private async Task FetchAndUpdatePricesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching Nordpool prices...");

        // TODO: Replace with actual Nordpool API call
        // For now, generate mock data
        var prices = GenerateMockPrices();
        
        _priceService.UpdatePrices(prices);
        
        _logger.LogInformation("Successfully fetched and updated {Count} prices", prices.Count());
        
        await Task.CompletedTask;
    }

    private IEnumerable<ElectricityPrice> GenerateMockPrices()
    {
        var now = DateTime.UtcNow;
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        
        var prices = new List<ElectricityPrice>();
        var random = new Random();

        for (int hour = 0; hour < 24; hour++)
        {
            var startTime = startOfDay.AddHours(hour);
            var endTime = startTime.AddHours(1);
            
            prices.Add(new ElectricityPrice
            {
                StartTime = startTime,
                EndTime = endTime,
                PricePerKwh = Math.Round((decimal)(random.NextDouble() * 2 + 0.5), 4),
                Currency = "EUR",
                Area = "NO1"
            });
        }

        return prices;
    }
}
