using Microsoft.AspNetCore.Http.HttpResults;
using NordpoolApi.Models;
using NordpoolApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Nordpool API";
        document.Info.Version = "v1";
        document.Info.Description = "API for retrieving electricity prices from Nordpool for Norwegian price zones (NO1-NO5)";
        document.Info.Contact = new()
        {
            Name = "Nordpool API",
            Url = new Uri("https://github.com/epaulsen/nordpool-api")
        };
        return Task.CompletedTask;
    });
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
app.MapOpenApi();

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
.WithSummary("Get today's electricity prices")
.WithOpenApi();

app.MapGet("/api/{zone}/all", async Task<Results<Ok<IEnumerable<ElectricityPrice>>, NotFound>> (string zone, IPriceService priceService) =>
{
    var prices = await priceService.GetAllPricesSortedAsync(zone);
    
    if (!prices.Any())
    {
        return TypedResults.NotFound();
    }
    
    return TypedResults.Ok(prices);
})
.WithName("GetAllElectricityPricesSorted")
.WithDescription("Get all electricity prices sorted by start time in ascending order for a specific zone")
.WithSummary("Get all electricity prices sorted")
.WithOpenApi();

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
.WithSummary("Get current electricity price")
.WithOpenApi();

app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithDescription("Health check endpoint")
    .WithSummary("Health check")
    .WithOpenApi()
    .ExcludeFromDescription();

app.Run();

// Make the implicit Program class public for tests
public partial class Program { }
