namespace NordpoolApi.Models;

public record ElectricityPrice
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public decimal PricePerKwh { get; init; }
    public string Currency { get; init; } = "EUR";
    public string Area { get; init; } = "NO1";
}
