namespace NordpoolApi.Models;

public record ElectricityPrice
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "NOK";
    public string Area { get; init; } = "NO1";
    public IList<QuarterlyPrice>? QuarterlyPrices { get; init; }
}

public record QuarterlyPrice
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public decimal Price { get; init; }
}
