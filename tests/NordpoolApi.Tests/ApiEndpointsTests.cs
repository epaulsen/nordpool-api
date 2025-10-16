using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task OpenApiEndpoint_ReturnsValidOpenApiSpecification()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify it's valid JSON
        var openApiDoc = JsonDocument.Parse(content);
        
        // Verify it has the required OpenAPI structure
        Assert.True(openApiDoc.RootElement.TryGetProperty("openapi", out var openApiVersion));
        Assert.Equal("3.0.1", openApiVersion.GetString());
        
        // Verify it has info section
        Assert.True(openApiDoc.RootElement.TryGetProperty("info", out var info));
        Assert.True(info.TryGetProperty("title", out var title));
        Assert.Equal("Nordpool API", title.GetString());
        
        // Verify it has paths section
        Assert.True(openApiDoc.RootElement.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/api/{zone}/prices", out _));
        Assert.True(paths.TryGetProperty("/api/{zone}/all", out _));
        Assert.True(paths.TryGetProperty("/api/{zone}/prices/current", out _));
        
        // Verify it has components/schemas section with ElectricityPrice
        Assert.True(openApiDoc.RootElement.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.TryGetProperty("ElectricityPrice", out _));
        Assert.True(schemas.TryGetProperty("QuarterlyPrice", out _));
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
        var response = await _client.GetAsync("/api/NO1/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        // Should return at least 24 prices (today), may return 48 if after 3 PM Norwegian time (today + tomorrow)
        Assert.True(prices.Count >= 24, $"Expected at least 24 prices, but got {prices.Count}");
    }

    [Fact]
    public async Task GetAllPrices_ReturnsSortedByStartTimeAscending()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        
        // Verify prices are sorted by Start time in ascending order
        for (int i = 1; i < prices.Count; i++)
        {
            Assert.True(prices[i - 1].Start <= prices[i].Start, 
                $"Prices should be sorted in ascending order by Start time. " +
                $"Found {prices[i - 1].Start} at index {i - 1} followed by {prices[i].Start} at index {i}");
        }
    }

    [Fact]
    public async Task GetCurrentPrice_ReturnsCurrentPrice()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/prices/current");

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
        var response = await _client.GetAsync("/api/NO1/prices/current?includeVAT=false");

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
        var responseWithoutVAT = await _client.GetAsync("/api/NO1/prices/current?includeVAT=false");
        var priceWithoutVAT = await responseWithoutVAT.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(priceWithoutVAT);
        
        // Act - Get the price with VAT
        var responseWithVAT = await _client.GetAsync("/api/NO1/prices/current?includeVAT=true");

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
        var response = await _client.GetAsync("/api/NO1/prices");

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
        var response = await _client.GetAsync("/api/NO1/prices/current");

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

    [Fact]
    public async Task GetAllPrices_IncludesSubsidizedPrice()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        
        // Verify each price has a subsidized price
        foreach (var price in prices)
        {
            // SubsidizedPrice should always be set (never 0)
            Assert.True(price.SubsidizedPrice > 0, "SubsidizedPrice should be greater than 0");
            
            // If price <= 0.75, subsidized price should equal the price
            if (price.Price <= 0.75m)
            {
                Assert.Equal(price.Price, price.SubsidizedPrice);
            }
            else
            {
                // If price > 0.75, subsidized price should be calculated correctly
                var expectedSubsidizedPrice = 0.75m + 0.1m * (price.Price - 0.75m);
                Assert.Equal(expectedSubsidizedPrice, price.SubsidizedPrice);
                // SubsidizedPrice should always be less than the original price when subsidy applies
                Assert.True(price.SubsidizedPrice < price.Price);
            }
        }
    }

    [Fact]
    public async Task GetCurrentPrice_IncludesSubsidizedPrice()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/prices/current");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var price = await response.Content.ReadFromJsonAsync<ElectricityPrice>();
        Assert.NotNull(price);
        
        // Verify price has a subsidized price
        Assert.True(price.SubsidizedPrice > 0, "SubsidizedPrice should be greater than 0");
        
        // If price <= 0.75, subsidized price should equal the price
        if (price.Price <= 0.75m)
        {
            Assert.Equal(price.Price, price.SubsidizedPrice);
        }
        else
        {
            // If price > 0.75, subsidized price should be calculated correctly
            var expectedSubsidizedPrice = 0.75m + 0.1m * (price.Price - 0.75m);
            Assert.Equal(expectedSubsidizedPrice, price.SubsidizedPrice);
            // SubsidizedPrice should always be less than the original price when subsidy applies
            Assert.True(price.SubsidizedPrice < price.Price);
        }
    }

    [Fact]
    public async Task GetAllPricesSorted_ReturnsAllPricesInAscendingOrder()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/all");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        
        // Verify prices are sorted by Start time in ascending order
        for (int i = 1; i < prices.Count; i++)
        {
            Assert.True(prices[i - 1].Start <= prices[i].Start, 
                $"Prices should be sorted in ascending order. Price at index {i - 1} has Start time {prices[i - 1].Start}, " +
                $"but price at index {i} has Start time {prices[i].Start}");
        }
        
        // Should return at least 24 prices (today), may return 48 if after 3 PM Norwegian time (today + tomorrow)
        Assert.True(prices.Count >= 24, $"Expected at least 24 prices, but got {prices.Count}");
    }

    [Fact]
    public async Task GetAllPricesSorted_ReturnsAllPricesFromDictionary()
    {
        // Act
        var responseAll = await _client.GetAsync("/api/NO1/all");
        var responseCurrent = await _client.GetAsync("/api/NO1/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, responseAll.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseCurrent.StatusCode);
        
        var allPricesSorted = await responseAll.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        var currentPrices = await responseCurrent.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        
        Assert.NotNull(allPricesSorted);
        Assert.NotNull(currentPrices);
        
        // Both endpoints should return the same number of prices (all prices in the dictionary)
        Assert.Equal(currentPrices.Count, allPricesSorted.Count);
    }

    [Fact]
    public async Task GetPrices_WithInvalidZone_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/NO99/prices");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllPricesSorted_WithInvalidZone_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/NO99/all");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPrices_WithValidZone_ReturnsOnlyPricesForThatZone()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        
        // Verify all prices are for NO1
        Assert.All(prices, price => Assert.Equal("NO1", price.Area));
    }

    [Fact]
    public async Task GetAllPricesSorted_WithValidZone_ReturnsOnlyPricesForThatZone()
    {
        // Act
        var response = await _client.GetAsync("/api/NO1/all");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var prices = await response.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        Assert.NotNull(prices);
        Assert.NotEmpty(prices);
        
        // Verify all prices are for NO1
        Assert.All(prices, price => Assert.Equal("NO1", price.Area));
    }

    [Fact]
    public async Task GetPrices_DifferentZones_ReturnDifferentPrices()
    {
        // Act
        var responseNO1 = await _client.GetAsync("/api/NO1/prices");
        var responseNO2 = await _client.GetAsync("/api/NO2/prices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, responseNO1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseNO2.StatusCode);
        
        var pricesNO1 = await responseNO1.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        var pricesNO2 = await responseNO2.Content.ReadFromJsonAsync<List<ElectricityPrice>>();
        
        Assert.NotNull(pricesNO1);
        Assert.NotNull(pricesNO2);
        Assert.NotEmpty(pricesNO1);
        Assert.NotEmpty(pricesNO2);
        
        // Verify all prices are for the correct zones
        Assert.All(pricesNO1, price => Assert.Equal("NO1", price.Area));
        Assert.All(pricesNO2, price => Assert.Equal("NO2", price.Area));
        
        // Verify the prices are different (NO2 should be 0.005 kr/kWh higher based on test data: 5 NOK/MWh = 0.005 kr/kWh)
        Assert.NotEqual(pricesNO1[0].Price, pricesNO2[0].Price);
        Assert.Equal(pricesNO1[0].Price + 0.005m, pricesNO2[0].Price);
    }

    [Fact]
    public async Task GetCurrentPrice_WithInvalidZone_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/NO99/prices/current");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentPrice_WithValidZone_ReturnsOnlyPricesForThatZone()
    {
        // Act
        var responseNO1 = await _client.GetAsync("/api/NO1/prices/current");
        var responseNO2 = await _client.GetAsync("/api/NO2/prices/current");

        // Assert
        Assert.Equal(HttpStatusCode.OK, responseNO1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseNO2.StatusCode);
        
        var priceNO1 = await responseNO1.Content.ReadFromJsonAsync<ElectricityPrice>();
        var priceNO2 = await responseNO2.Content.ReadFromJsonAsync<ElectricityPrice>();
        
        Assert.NotNull(priceNO1);
        Assert.NotNull(priceNO2);
        
        // Verify each price is for the correct zone
        Assert.Equal("NO1", priceNO1.Area);
        Assert.Equal("NO2", priceNO2.Area);
        
        // Verify the prices are different for different zones
        Assert.NotEqual(priceNO1.Price, priceNO2.Price);
    }
}
