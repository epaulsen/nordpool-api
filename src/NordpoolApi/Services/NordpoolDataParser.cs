using System.Text.Json;
using NordpoolApi.Models;

namespace NordpoolApi.Services;

public class NordpoolDataParser
{
    /// <summary>
    /// Parses Nordpool JSON data and extracts electricity prices from multiAreaEntries.
    /// Converts prices from MWh to kWh by dividing by 1000.
    /// </summary>
    /// <param name="jsonData">JSON string containing Nordpool data</param>
    /// <returns>Collection of ElectricityPrice objects</returns>
    public IEnumerable<ElectricityPrice> ParsePrices(string jsonData)
    {
        var nordpoolData = JsonSerializer.Deserialize<NordpoolData>(jsonData);
        
        if (nordpoolData?.MultiAreaEntries == null)
        {
            return Enumerable.Empty<ElectricityPrice>();
        }

        var prices = new List<ElectricityPrice>();
        
        foreach (var entry in nordpoolData.MultiAreaEntries)
        {
            if (entry.EntryPerArea == null)
            {
                continue;
            }
            
            foreach (var areaPrice in entry.EntryPerArea)
            {
                // Convert from MWh to kWh by dividing by 1000
                var priceInKwh = areaPrice.Value / 1000m;
                
                prices.Add(new ElectricityPrice
                {
                    Start = entry.DeliveryStart,
                    End = entry.DeliveryEnd,
                    Price = priceInKwh,
                    Currency = nordpoolData.Currency ?? "NOK",
                    Area = areaPrice.Key
                });
            }
        }
        
        return prices;
    }
}
