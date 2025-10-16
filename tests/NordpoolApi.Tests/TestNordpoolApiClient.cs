using NordpoolApi.Services;

namespace NordpoolApi.Tests;

public class TestNordpoolApiClient : INordpoolApiClient
{
    public Task<string?> FetchPriceDataAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        // Generate mock data for today's date
        return Task.FromResult<string?>(GenerateMockJsonData(date));
    }
    
    private string GenerateMockJsonData(DateOnly date)
    {
        var entries = new List<string>();
        var startDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        
        // Generate hourly data (24 hours) for one area (NO1) to match test expectations
        for (int hour = 0; hour < 24; hour++)
        {
            var deliveryStart = startDate.AddHours(hour);
            var deliveryEnd = deliveryStart.AddHours(1);
            
            var entry = $@"{{
                ""deliveryStart"": ""{deliveryStart:yyyy-MM-ddTHH:mm:ssZ}"",
                ""deliveryEnd"": ""{deliveryEnd:yyyy-MM-ddTHH:mm:ssZ}"",
                ""entryPerArea"": {{
                    ""NO1"": {100.0 + hour * 10}
                }}
            }}";
            
            entries.Add(entry);
        }
        
        return $@"{{
            ""deliveryDateCET"": ""{date:yyyy-MM-dd}"",
            ""currency"": ""NOK"",
            ""multiAreaEntries"": [{string.Join(",", entries)}]
        }}";
    }
}
