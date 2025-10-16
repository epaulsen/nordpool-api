using System.Text.Json;
using NordpoolApi.Models;

namespace NordpoolApi.Services;

public class NordpoolDataParser
{
    /// <summary>
    /// Parses Nordpool JSON data and extracts electricity prices from multiAreaEntries.
    /// Converts prices from MWh to kWh by dividing by 1000.
    /// Groups 15-minute intervals into hourly averages.
    /// </summary>
    /// <param name="jsonData">JSON string containing Nordpool data</param>
    /// <returns>Collection of ElectricityPrice objects with hourly averages</returns>
    public IEnumerable<ElectricityPrice> ParsePrices(string jsonData)
    {
        var nordpoolData = JsonSerializer.Deserialize<NordpoolData>(jsonData);
        
        if (nordpoolData?.MultiAreaEntries == null)
        {
            return Enumerable.Empty<ElectricityPrice>();
        }

        // First, collect all quarterly prices by area and hour
        var quarterlyPricesByAreaAndHour = new Dictionary<(string Area, DateTime HourStart), List<(DateTime Start, DateTime End, decimal Price)>>();
        
        foreach (var entry in nordpoolData.MultiAreaEntries)
        {
            if (entry.EntryPerArea == null)
            {
                continue;
            }
            
            // Get the hour start (truncate to the hour)
            var hourStart = new DateTime(entry.DeliveryStart.Year, entry.DeliveryStart.Month, entry.DeliveryStart.Day, 
                                        entry.DeliveryStart.Hour, 0, 0, entry.DeliveryStart.Kind);
            
            foreach (var areaPrice in entry.EntryPerArea)
            {
                var key = (areaPrice.Key, hourStart);
                if (!quarterlyPricesByAreaAndHour.ContainsKey(key))
                {
                    quarterlyPricesByAreaAndHour[key] = new List<(DateTime, DateTime, decimal)>();
                }
                
                // Convert from MWh to kWh by dividing by 1000
                var priceInKwh = areaPrice.Value / 1000m;
                quarterlyPricesByAreaAndHour[key].Add((entry.DeliveryStart, entry.DeliveryEnd, priceInKwh));
            }
        }
        
        // Now compute hourly averages
        var hourlyPrices = new List<ElectricityPrice>();
        
        foreach (var kvp in quarterlyPricesByAreaAndHour)
        {
            var (area, hourStart) = kvp.Key;
            var quarterlyPrices = kvp.Value.OrderBy(p => p.Start).ToList();
            
            // Calculate hourly average
            var averagePrice = quarterlyPrices.Average(p => p.Price);
            
            // Determine hour end (should be 1 hour after start)
            var hourEnd = hourStart.AddHours(1);
            
            // Create quarterly price objects
            var quarterlyPriceObjects = quarterlyPrices.Select(p => new QuarterlyPrice
            {
                Start = p.Start,
                End = p.End,
                Price = p.Price
            }).ToList();
            
            // Calculate subsidized price according to Norwegian government policy
            // If price > 0.75 kr: government pays 90% of amount above 0.75 kr
            // Consumer pays: 0.75 + 0.1 * (price - 0.75)
            decimal subsidizedPrice;
            if (averagePrice > 0.75m)
            {
                subsidizedPrice = 0.75m + 0.1m * (averagePrice - 0.75m);
            }
            else
            {
                subsidizedPrice = averagePrice;
            }
            
            hourlyPrices.Add(new ElectricityPrice
            {
                Start = hourStart,
                End = hourEnd,
                Price = averagePrice,
                SubsidizedPrice = subsidizedPrice,
                Currency = nordpoolData.Currency ?? "NOK",
                Area = area,
                QuarterlyPrices = quarterlyPriceObjects
            });
        }
        
        return hourlyPrices.OrderBy(p => p.Start).ThenBy(p => p.Area);
    }
}
