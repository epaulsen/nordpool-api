namespace NordpoolApi.Models;

public record HealthCheckResponse
{
    public string Status { get; init; } = "healthy";
    public DateTime Timestamp { get; init; }
}
