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
        
        // The sample data has 24 hours and 5 areas = 120 hourly averages
        Assert.Equal(120, prices.Count);
    }

    [Fact]
    public void ParsePrices_ConvertsFromMWhToKWh()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert - First hour in sample data has NO1 average of (726.47 + 723.89 + 721.66 + 719.55) / 4 = 722.8925 MWh
        var firstNo1Price = prices.First(p => p.Area == "NO1");
        Assert.Equal(0.7228925m, firstNo1Price.Price); // 722.8925 / 1000 = 0.7228925
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
        Assert.Equal(new DateTime(2025, 10, 16, 23, 0, 0, DateTimeKind.Utc), firstPrice.End);
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

    [Fact]
    public void ParsePrices_IncludesQuarterlyPrices()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        var firstPrice = prices.First();
        Assert.NotNull(firstPrice.QuarterlyPrices);
        Assert.Equal(4, firstPrice.QuarterlyPrices.Count); // 4 quarterly prices per hour
        
        // Verify the quarterly prices are in correct order
        var quarterlyPrices = firstPrice.QuarterlyPrices.ToList();
        Assert.Equal(new DateTime(2025, 10, 16, 22, 0, 0, DateTimeKind.Utc), quarterlyPrices[0].Start);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 15, 0, DateTimeKind.Utc), quarterlyPrices[0].End);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 15, 0, DateTimeKind.Utc), quarterlyPrices[1].Start);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 30, 0, DateTimeKind.Utc), quarterlyPrices[1].End);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 30, 0, DateTimeKind.Utc), quarterlyPrices[2].Start);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 45, 0, DateTimeKind.Utc), quarterlyPrices[2].End);
        Assert.Equal(new DateTime(2025, 10, 16, 22, 45, 0, DateTimeKind.Utc), quarterlyPrices[3].Start);
        Assert.Equal(new DateTime(2025, 10, 16, 23, 0, 0, DateTimeKind.Utc), quarterlyPrices[3].End);
    }

    [Fact]
    public void ParsePrices_HourlyPriceIsAverageOfQuarterlyPrices()
    {
        // Arrange
        var testDataPath = Path.Combine("testdata", "sampledata.json");
        var jsonData = File.ReadAllText(testDataPath);

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        var firstPrice = prices.First();
        Assert.NotNull(firstPrice.QuarterlyPrices);
        
        var quarterlyAverage = firstPrice.QuarterlyPrices.Average(q => q.Price);
        Assert.Equal(firstPrice.Price, quarterlyAverage);
    }

    [Fact]
    public void ParsePrices_CalculatesSubsidizedPrice_WhenPriceAboveThreshold()
    {
        // Arrange - Create JSON with price above 0.75 kr (1.0 kr)
        var jsonData = @"{
            ""deliveryDateCET"": ""2025-10-17"",
            ""currency"": ""NOK"",
            ""multiAreaEntries"": [
                {
                    ""deliveryStart"": ""2025-10-16T22:00:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:15:00Z"",
                    ""entryPerArea"": { ""NO1"": 1000.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:15:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:30:00Z"",
                    ""entryPerArea"": { ""NO1"": 1000.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:30:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:45:00Z"",
                    ""entryPerArea"": { ""NO1"": 1000.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:45:00Z"",
                    ""deliveryEnd"": ""2025-10-16T23:00:00Z"",
                    ""entryPerArea"": { ""NO1"": 1000.0 }
                }
            ]
        }";

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.Single(prices);
        var price = prices.First();
        Assert.Equal(1.0m, price.Price); // 1000 / 1000 = 1.0 kr
        // SubsidizedPrice = 0.75 + 0.1 * (1.0 - 0.75) = 0.75 + 0.025 = 0.775
        Assert.Equal(0.775m, price.SubsidizedPrice);
    }

    [Fact]
    public void ParsePrices_CalculatesSubsidizedPrice_WhenPriceBelowThreshold()
    {
        // Arrange - Create JSON with price below 0.75 kr (0.5 kr)
        var jsonData = @"{
            ""deliveryDateCET"": ""2025-10-17"",
            ""currency"": ""NOK"",
            ""multiAreaEntries"": [
                {
                    ""deliveryStart"": ""2025-10-16T22:00:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:15:00Z"",
                    ""entryPerArea"": { ""NO1"": 500.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:15:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:30:00Z"",
                    ""entryPerArea"": { ""NO1"": 500.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:30:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:45:00Z"",
                    ""entryPerArea"": { ""NO1"": 500.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:45:00Z"",
                    ""deliveryEnd"": ""2025-10-16T23:00:00Z"",
                    ""entryPerArea"": { ""NO1"": 500.0 }
                }
            ]
        }";

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.Single(prices);
        var price = prices.First();
        Assert.Equal(0.5m, price.Price); // 500 / 1000 = 0.5 kr
        Assert.Equal(0.5m, price.SubsidizedPrice); // Below threshold, so same as price
    }

    [Fact]
    public void ParsePrices_CalculatesSubsidizedPrice_WhenPriceExactlyAtThreshold()
    {
        // Arrange - Create JSON with price exactly at 0.75 kr
        var jsonData = @"{
            ""deliveryDateCET"": ""2025-10-17"",
            ""currency"": ""NOK"",
            ""multiAreaEntries"": [
                {
                    ""deliveryStart"": ""2025-10-16T22:00:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:15:00Z"",
                    ""entryPerArea"": { ""NO1"": 750.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:15:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:30:00Z"",
                    ""entryPerArea"": { ""NO1"": 750.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:30:00Z"",
                    ""deliveryEnd"": ""2025-10-16T22:45:00Z"",
                    ""entryPerArea"": { ""NO1"": 750.0 }
                },
                {
                    ""deliveryStart"": ""2025-10-16T22:45:00Z"",
                    ""deliveryEnd"": ""2025-10-16T23:00:00Z"",
                    ""entryPerArea"": { ""NO1"": 750.0 }
                }
            ]
        }";

        // Act
        var prices = _parser.ParsePrices(jsonData).ToList();

        // Assert
        Assert.Single(prices);
        var price = prices.First();
        Assert.Equal(0.75m, price.Price); // 750 / 1000 = 0.75 kr
        Assert.Equal(0.75m, price.SubsidizedPrice); // At threshold, so same as price
    }
}
