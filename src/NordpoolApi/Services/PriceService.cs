using System.Collections.Concurrent;
using NordpoolApi.Models;

namespace NordpoolApi.Services;

public class PriceService : IPriceService
{
    private readonly ConcurrentBag<ElectricityPrice> _prices = new();
    private readonly ILogger<PriceService> _logger;

    public PriceService(ILogger<PriceService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync()
    {
        return Task.FromResult(_prices.AsEnumerable());
    }

    public Task<ElectricityPrice?> GetCurrentPriceAsync()
    {
        var now = DateTime.UtcNow;
        var currentPrice = _prices
            .Where(p => p.StartTime <= now && p.EndTime > now)
            .OrderByDescending(p => p.StartTime)
            .FirstOrDefault();

        return Task.FromResult(currentPrice);
    }

    public void UpdatePrices(IEnumerable<ElectricityPrice> prices)
    {
        _prices.Clear();
        foreach (var price in prices)
        {
            _prices.Add(price);
        }
        _logger.LogInformation("Updated {Count} prices", prices.Count());
    }
}
