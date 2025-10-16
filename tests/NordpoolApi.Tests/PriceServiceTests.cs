using Microsoft.Extensions.Logging;
using Moq;
using NordpoolApi.Models;
using NordpoolApi.Services;
using Xunit;

namespace NordpoolApi.Tests;

public class PriceServiceTests
{
    private readonly PriceService _priceService;

    public PriceServiceTests()
    {
        var loggerMock = new Mock<ILogger<PriceService>>();
        _priceService = new PriceService(loggerMock.Object);
    }

    [Fact]
    public async Task AddPrices_AddsNewPrices()
    {
        // Arrange
        var prices = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = DateTime.UtcNow,
                End = DateTime.UtcNow.AddHours(1),
                Price = 0.5m,
                SubsidizedPrice = 0.5m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        // Act
        _priceService.AddPrices(prices);
        var result = await _priceService.GetCurrentPricesAsync();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task RemoveOldPrices_RemovesPricesBeforeCutoff()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var prices = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = now.AddHours(-2),
                End = now.AddHours(-1),
                Price = 0.5m,
                SubsidizedPrice = 0.5m,
                Currency = "NOK",
                Area = "NO1"
            },
            new ElectricityPrice
            {
                Start = now,
                End = now.AddHours(1),
                Price = 0.6m,
                SubsidizedPrice = 0.6m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        _priceService.AddPrices(prices);

        // Act
        _priceService.RemoveOldPrices(now);
        var result = await _priceService.GetCurrentPricesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(0.6m, result.First().Price);
    }

    [Fact]
    public async Task UpdatePrices_ReplacesAllPrices()
    {
        // Arrange
        var initialPrices = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = DateTime.UtcNow,
                End = DateTime.UtcNow.AddHours(1),
                Price = 0.5m,
                SubsidizedPrice = 0.5m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        var newPrices = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = DateTime.UtcNow.AddHours(1),
                End = DateTime.UtcNow.AddHours(2),
                Price = 0.7m,
                SubsidizedPrice = 0.7m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        _priceService.AddPrices(initialPrices);

        // Act
        _priceService.UpdatePrices(newPrices);
        var result = await _priceService.GetCurrentPricesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(0.7m, result.First().Price);
    }

    [Fact]
    public async Task GetCurrentPriceAsync_ReturnsCurrentPrice()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var prices = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = now.AddMinutes(-30),
                End = now.AddMinutes(30),
                Price = 0.5m,
                SubsidizedPrice = 0.5m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        _priceService.AddPrices(prices);

        // Act
        var result = await _priceService.GetCurrentPriceAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.5m, result.Price);
    }

    [Fact]
    public async Task AddPrices_HandlesDuplicateKeys()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var prices1 = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = now,
                End = now.AddHours(1),
                Price = 0.5m,
                SubsidizedPrice = 0.5m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        var prices2 = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = now,
                End = now.AddHours(1),
                Price = 0.6m,
                SubsidizedPrice = 0.6m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        // Act
        _priceService.AddPrices(prices1);
        _priceService.AddPrices(prices2); // Should not add duplicate

        var result = await _priceService.GetCurrentPricesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(0.5m, result.First().Price); // Should keep the first one
    }

    [Fact]
    public async Task GetCurrentPricesAsync_ReturnsPricesSortedByStartTimeAscending()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 16, 0, 0, 0, DateTimeKind.Utc);
        var prices = new List<ElectricityPrice>
        {
            new ElectricityPrice
            {
                Start = baseTime.AddHours(3),
                End = baseTime.AddHours(4),
                Price = 0.7m,
                SubsidizedPrice = 0.7m,
                Currency = "NOK",
                Area = "NO1"
            },
            new ElectricityPrice
            {
                Start = baseTime.AddHours(1),
                End = baseTime.AddHours(2),
                Price = 0.5m,
                SubsidizedPrice = 0.5m,
                Currency = "NOK",
                Area = "NO1"
            },
            new ElectricityPrice
            {
                Start = baseTime.AddHours(2),
                End = baseTime.AddHours(3),
                Price = 0.6m,
                SubsidizedPrice = 0.6m,
                Currency = "NOK",
                Area = "NO1"
            }
        };

        // Act - Add prices in random order
        _priceService.AddPrices(prices);
        var result = await _priceService.GetCurrentPricesAsync();

        // Assert - Verify prices are sorted by Start time
        var sortedPrices = result.ToList();
        Assert.Equal(3, sortedPrices.Count);
        
        Assert.Equal(baseTime.AddHours(1), sortedPrices[0].Start);
        Assert.Equal(0.5m, sortedPrices[0].Price);
        
        Assert.Equal(baseTime.AddHours(2), sortedPrices[1].Start);
        Assert.Equal(0.6m, sortedPrices[1].Price);
        
        Assert.Equal(baseTime.AddHours(3), sortedPrices[2].Start);
        Assert.Equal(0.7m, sortedPrices[2].Price);
    }
}
