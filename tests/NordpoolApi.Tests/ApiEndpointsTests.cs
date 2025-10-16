using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NordpoolApi.Models;
using Xunit;

namespace NordpoolApi.Tests;

public class ApiEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }

    [Fact]
    public async Task GetAllPrices_ReturnsListOfPrices()
    {
        // Act
        var response = await _client.GetAsync("/api/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        // Should return at least 24 prices (today), may return 48 if after 3 PM Norwegian time (today + tomorrow)
        Assert.True(prices.Count >= 24, $"Expected at least 24 prices, but got {prices.Count}");
    }

    [Fact]
    public async Task GetCurrentPrice_ReturnsCurrentPrice()
    {
        // Act
        var response = await _client.GetAsync("/api/prices/current");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var price = await response.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(price);
        Assert.True(price.Price > 0);
        Assert.Equal("NOK", price.Currency);
        Assert.Equal("NO1", price.Area);
        
        // Verify the price is for the current hour
        var now = DateTime.UtcNow;
        Assert.True(price.Start <= now);
        Assert.True(price.End > now);
    }

    [Fact]
    public async Task GetCurrentPrice_WithIncludeVATFalse_ReturnsOriginalPrice()
    {
        // Act
        var response = await _client.GetAsync("/api/prices/current?includeVAT=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var price = await response.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(price);
        Assert.True(price.Price > 0);
        Assert.Equal("NOK", price.Currency);
        Assert.Equal("NO1", price.Area);
    }

    [Fact]
    public async Task GetCurrentPrice_WithIncludeVATTrue_ReturnsPriceWithVAT()
    {
        // Arrange - Get the price without VAT first
        var responseWithoutVAT = await _client.GetAsync("/api/prices/current?includeVAT=false");
        var priceWithoutVAT = await responseWithoutVAT.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(priceWithoutVAT);
        
        // Act - Get the price with VAT
        var responseWithVAT = await _client.GetAsync("/api/prices/current?includeVAT=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, responseWithVAT.StatusCode);
        
        var priceWithVAT = await responseWithVAT.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(priceWithVAT);
        Assert.True(priceWithVAT.Price > 0);
        
        // Verify the price with VAT is 1.25 times the price without VAT
        var expectedPriceWithVAT = priceWithoutVAT.Price * 1.25m;
        Assert.Equal(expectedPriceWithVAT, priceWithVAT.Price);
        
        // Verify other properties remain the same
        Assert.Equal(priceWithoutVAT.Start, priceWithVAT.Start);
        Assert.Equal(priceWithoutVAT.End, priceWithVAT.End);
        Assert.Equal(priceWithoutVAT.Currency, priceWithVAT.Currency);
        Assert.Equal(priceWithoutVAT.Area, priceWithVAT.Area);
    }

    [Fact]
    public async Task GetAllPrices_ReturnsHourlyAveragesWithQuarterlyPrices()
    {
        // Act
        var response = await _client.GetAsync("/api/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        
        // Check that the first price has quarterly prices
        var firstPrice = prices.First();
        Assert.NotNull(firstPrice.QuarterlyPrices);
        Assert.Equal(4, firstPrice.QuarterlyPrices.Count);
        
        // Verify the price is the average of quarterly prices
        var expectedAverage = firstPrice.QuarterlyPrices.Average(q => q.Price);
        Assert.Equal(expectedAverage, firstPrice.Price);
        
        // Verify the time span is exactly one hour
        var hourSpan = firstPrice.End - firstPrice.Start;
        Assert.Equal(1, hourSpan.TotalHours);
        
        // Verify quarterly prices cover the entire hour in 15-minute intervals
        var quarterlyPrices = firstPrice.QuarterlyPrices.OrderBy(q => q.Start).ToList();
        for (int i = 0; i < 4; i++)
        {
            var quarterSpan = quarterlyPrices[i].End - quarterlyPrices[i].Start;
            Assert.Equal(15, quarterSpan.TotalMinutes);
            
            // Verify continuity
            if (i > 0)
            {
                Assert.Equal(quarterlyPrices[i - 1].End, quarterlyPrices[i].Start);
            }
        }
        
        // Verify first quarter starts at hour start
        Assert.Equal(firstPrice.Start, quarterlyPrices[0].Start);
        
        // Verify last quarter ends at hour end
        Assert.Equal(firstPrice.End, quarterlyPrices[3].End);
    }

    [Fact]
    public async Task GetCurrentPrice_ReturnsHourlyAverageWithQuarterlyPrices()
    {
        // Act
        var response = await _client.GetAsync("/api/prices/current");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var price = await response.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(price);
        
        // Check that the price has quarterly prices
        Assert.NotNull(price.QuarterlyPrices);
        Assert.Equal(4, price.QuarterlyPrices.Count);
        
        // Verify the price is the average of quarterly prices
        var expectedAverage = price.QuarterlyPrices.Average(q => q.Price);
        Assert.Equal(expectedAverage, price.Price);
        
        // Verify the time span is exactly one hour
        var hourSpan = price.End - price.Start;
        Assert.Equal(1, hourSpan.TotalHours);
    }
}
