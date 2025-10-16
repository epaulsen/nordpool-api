using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NordpoolApi.Models;
using Xunit;

namespace NordpoolApi.Tests;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
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
        Assert.Equal(24, prices.Count);
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
}
