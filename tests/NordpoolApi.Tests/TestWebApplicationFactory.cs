using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NordpoolApi.Services;

namespace NordpoolApi.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real API client registration
            services.RemoveAll<INordpoolApiClient>();
            
            // Add test API client
            services.AddSingleton<INordpoolApiClient, TestNordpoolApiClient>();
        });
    }
}
