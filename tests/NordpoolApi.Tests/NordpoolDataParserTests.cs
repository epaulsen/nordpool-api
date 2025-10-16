using NordpoolApi.Services;
using Xunit;

namespace NordpoolApi.Tests;

public class NordpoolDataParserTests
{
    private readonly NordpoolDataParser _parser;

    public NordpoolDataParserTests()
    {
        _parser = new NordpoolDataParser();
    }

    [Fact]
    public void ParsePrices_WithValidData_ReturnsElectricityPrices()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.NotEmpty(prices);
        
        // The sample data has 96 entries (15-minute intervals for 24 hours) and 5 areas = 480 prices
        Assert.Equal(480, prices.Count);
    }

    [Fact]
    public void ParsePrices_ConvertsFromMWhToKWh()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert - First entry in sample data has NO1 = 726.47 MWh
        var firstNo1Price = prices.First(p => p.Area == "NO1");
        Assert.Equal(0.72647m, firstNo1Price.Price); // 726.47 / 1000 = 0.72647
    }

    [Fact]
    public void ParsePrices_ExtractsCorrectAreaData()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        var areas = prices.Select(p => p.Area).Distinct().OrderBy(a => a).ToList();
        Assert.Equal(5, areas.Count);
        Assert.Contains("NO1", areas);
        Assert.Contains("NO2", areas);
        Assert.Contains("NO3", areas);
        Assert.Contains("NO4", areas);
        Assert.Contains("NO5", areas);
    }

    [Fact]
    public void ParsePrices_ExtractsCorrectTimeData()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        var firstPrice = prices.First();
        Assert.Equal(new DateTime(2025, 10, 16, 22, 0, 0, DateTimeKind.Utc), firstPrice.Start);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 15, 0, DateTimeKind.Utc), firstPrice.End);
    }

    [Fact]
    public void ParsePrices_ExtractsCorrectCurrency()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.All(prices, price => Assert.Equal("NOK", price.Currency));
    }

    [Fact]
    public void ParsePrices_WithEmptyJson_ReturnsEmptyCollection()
    {
        // Arrange
        var jsonData = "{}";

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.Empty(prices);
    }

    [Fact]
    public void ParsePrices_WithNullMultiAreaEntries_ReturnsEmptyCollection()
    {
        // Arrange
        var jsonData = @"{
            ""deliveryDateCET"": ""2025-10-17"",
            ""currency"": ""NOK"",
            ""multiAreaEntries"": null
        }";

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.Empty(prices);
    }
}
