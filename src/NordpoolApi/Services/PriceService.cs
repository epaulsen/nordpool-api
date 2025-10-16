using System.Collections.Concurrent;
using NordpoolApi.Models;

namespace NordpoolApi.Services;

public class PriceService : IPriceService
{
    private readonly ConcurrentDictionary<(DateTime Start, string Area), ElectricityPrice> _prices = new();
    private readonly ILogger<PriceService> _logger;

    public PriceService(ILogger<PriceService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync()
    {
        return Task.FromResult(_prices.Values.OrderBy(p => p.Start).AsEnumerable());
    }

    public Task<ElectricityPrice?> GetCurrentPriceAsync()
    {
        var now = DateTime.UtcNow;
        var currentPrice = _prices.Values
            .Where(p => p.Start <= now && p.End > now)
            .OrderByDescending(p => p.Start)
            .FirstOrDefault();

        return Task.FromResult(currentPrice);
    }

    public Task<ElectricityPrice?> GetCurrentPriceAsync(string zone)
    {
        var now = DateTime.UtcNow;
        var currentPrice = _prices.Values
            .Where(p => p.Area.Equals(zone, StringComparison.OrdinalIgnoreCase) && p.Start <= now && p.End > now)
            .OrderByDescending(p => p.Start)
            .FirstOrDefault();

        return Task.FromResult(currentPrice);
    }

    public Task<IEnumerable<ElectricityPrice>> GetAllPricesSortedAsync()
    {
        var sortedPrices = _prices.Values.OrderBy(p => p.Start).AsEnumerable();
        return Task.FromResult(sortedPrices);
    }

    public Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync(string zone)
    {
        var prices = _prices.Values
            .Where(p => p.Area.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Start)
            .AsEnumerable();
        return Task.FromResult(prices);
    }

    public Task<IEnumerable<ElectricityPrice>> GetAllPricesSortedAsync(string zone)
    {
        var sortedPrices = _prices.Values
            .Where(p => p.Area.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Start)
            .AsEnumerable();
        return Task.FromResult(sortedPrices);
    }

    public void UpdatePrices(IEnumerable<ElectricityPrice> prices)
    {
        _prices.Clear();
        foreach (var price in prices)
        {
            _prices.TryAdd((price.Start, price.Area), price);
        }
        _logger.LogInformation("Updated {Count} prices", prices.Count());
    }

    public void AddPrices(IEnumerable<ElectricityPrice> prices)
    {
        foreach (var price in prices)
        {
            _prices.TryAdd((price.Start, price.Area), price);
        }
        _logger.LogInformation("Added {Count} prices", prices.Count());
    }

    public void RemoveOldPrices(DateTime cutoffTime)
    {
        var keysToRemove = _prices
            .Where(kvp => kvp.Value.End <= cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _prices.TryRemove(key, out _);
        }
        
        _logger.LogInformation("Removed {Count} old prices before {CutoffTime}", keysToRemove.Count, cutoffTime);
    }
}
