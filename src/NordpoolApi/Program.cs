using Microsoft.AspNetCore.Http.HttpResults;
using NordpoolApi.Models;
using NordpoolApi.Services;
using System.Text.Json.Serialization;
using System.Reflection;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization with source generation
// Use reflection to avoid compile-time dependency on generated code
builder.Services.ConfigureHttpJsonOptions(options =>
{
    var contextType = Type.GetType("NordpoolApi.AppJsonSerializerContext, NordpoolApi");
    if (contextType != null)
    {
        var defaultProperty = contextType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
        if (defaultProperty != null)
        {
            var context = (JsonSerializerContext)defaultProperty.GetValue(null)!;
            options.SerializerOptions.TypeInfoResolverChain.Add(context);
        }
    }
});

// Add Nordpool services
builder.Services.AddHttpClient<INordpoolApiClient, NordpoolApiClient>();
builder.Services.AddSingleton<NordpoolDataParser>();
builder.Services.AddSingleton<PriceService>();
builder.Services.AddSingleton<IPriceService>(sp => sp.GetRequiredService<PriceService>());
builder.Services.AddSingleton<IScheduler, Scheduler>();
builder.Services.AddHostedService<NordpoolPollingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Electricity prices endpoints
app.MapGet("/api/{zone}/prices", async Task<Results<Ok<IEnumerable<ElectricityPrice>>, NotFound>> (string zone, IPriceService priceService) =>
{
    var prices = await priceService.GetCurrentPricesAsync(zone);
    
    if (!prices.Any())
    {
        return TypedResults.NotFound();
    }
    
    return TypedResults.Ok(prices);
})
.WithName("GetElectricityPrices")
.WithDescription("Get all electricity prices for today for a specific zone")
.WithSummary("Get today's electricity prices");

app.MapGet("/api/{zone}/prices/current", async Task<Results<Ok<ElectricityPrice>, NotFound>> (string zone, IPriceService priceService, bool includeVAT = false) =>
{
    var currentPrice = await priceService.GetCurrentPriceAsync(zone);
    
    if (currentPrice == null)
    {
        return TypedResults.NotFound();
    }
    
    if (includeVAT)
    {
        currentPrice = currentPrice with { Price = currentPrice.Price * 1.25m };
    }
    
    return TypedResults.Ok(currentPrice);
})
.WithName("GetCurrentElectricityPrice")
.WithDescription("Get the current electricity price for a specific zone. Use includeVAT=true to include 25% VAT in the price.")
.WithSummary("Get current electricity price");

app.MapGet("/health", () => TypedResults.Ok(new HealthCheckResponse { Status = "healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithDescription("Health check endpoint")
    .WithSummary("Health check")
    .ExcludeFromDescription();

app.Run();

// Make the implicit Program class public for tests
public partial class Program { }
