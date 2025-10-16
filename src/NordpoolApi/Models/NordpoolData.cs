using System.Text.Json.Serialization;

namespace NordpoolApi.Models;

public class NordpoolData
{
    [JsonPropertyName("deliveryDateCET")]
    public string? DeliveryDateCET { get; set; }
    
    [JsonPropertyName("version")]
    public int Version { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
    
    [JsonPropertyName("deliveryAreas")]
    public List<string>? DeliveryAreas { get; set; }
    
    [JsonPropertyName("market")]
    public string? Market { get; set; }
    
    [JsonPropertyName("multiAreaEntries")]
    public List<MultiAreaEntry>? MultiAreaEntries { get; set; }
    
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
    
    [JsonPropertyName("exchangeRate")]
    public decimal ExchangeRate { get; set; }
}

public class MultiAreaEntry
{
    [JsonPropertyName("deliveryStart")]
    public DateTime DeliveryStart { get; set; }
    
    [JsonPropertyName("deliveryEnd")]
    public DateTime DeliveryEnd { get; set; }
    
    [JsonPropertyName("entryPerArea")]
    public Dictionary<string, decimal>? EntryPerArea { get; set; }
}
