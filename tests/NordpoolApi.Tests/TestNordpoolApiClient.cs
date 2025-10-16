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
        
        // Generate 15-minute interval data (96 intervals per day = 24 hours * 4 quarters) for one area (NO1)
        for (int hour = 0; hour < 24; hour++)
        {
            for (int quarter = 0; quarter < 4; quarter++)
            {
                var deliveryStart = startDate.AddHours(hour).AddMinutes(quarter * 15);
                var deliveryEnd = deliveryStart.AddMinutes(15);
                
                // Generate varying prices within each hour to simulate real data
                var basePrice = 100.0 + hour * 10;
                var quarterAdjustment = quarter * 0.5; // Small variation within the hour
                
                var entry = $@"{{
                    ""deliveryStart"": ""{deliveryStart:yyyy-MM-ddTHH:mm:ssZ}"",
                    ""deliveryEnd"": ""{deliveryEnd:yyyy-MM-ddTHH:mm:ssZ}"",
                    ""entryPerArea"": {{
                        ""NO1"": {basePrice + quarterAdjustment}
                    }}
                }}";
                
                entries.Add(entry);
            }
        }
        
        return $@"{{
            ""deliveryDateCET"": ""{date:yyyy-MM-dd}"",
            ""currency"": ""NOK"",
            ""multiAreaEntries"": [{string.Join(",", entries)}]
        }}";
    }
}
