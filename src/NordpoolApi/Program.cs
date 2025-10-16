using NordpoolApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Add Nordpool services
builder.Services.AddHttpClient<INordpoolApiClient, NordpoolApiClient>();
builder.Services.AddSingleton<NordpoolDataParser>();
builder.Services.AddSingleton<PriceService>();
builder.Services.AddSingleton<IPriceService>(sp => sp.GetRequiredService<PriceService>());
builder.Services.AddSingleton<IScheduler, Scheduler>();
builder.Services.AddHostedService<NordpoolPollingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Electricity prices endpoints
app.MapGet("/api/prices", async (IPriceService priceService) =>
{
    var prices = await priceService.GetCurrentPricesAsync();
    return Results.Ok(prices);
})
.WithName("GetElectricityPrices")
.WithDescription("Get all electricity prices for today");

app.MapGet("/api/prices/current", async (IPriceService priceService, bool includeVAT = false) =>
{
    var currentPrice = await priceService.GetCurrentPriceAsync();
    
    if (currentPrice == null)
    {
        return Results.NotFound(new { message = "No price data available for the current time" });
    }
    
    var priceValue = currentPrice.Price;
    if (includeVAT)
    {
        priceValue *= 1.25m;
    }
    
    return Results.Ok(priceValue);
})
.WithName("GetCurrentElectricityPrice")
.WithDescription("Get the current electricity price value. Use includeVAT=true to include 25% VAT.");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithDescription("Health check endpoint");

app.Run();

// Make the implicit Program class public for tests
public partial class Program { }
