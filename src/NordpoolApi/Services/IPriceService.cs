using NordpoolApi.Models;

namespace NordpoolApi.Services;

public interface IPriceService
{
    Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync();
    Task<ElectricityPrice?> GetCurrentPriceAsync();
    Task<ElectricityPrice?> GetCurrentPriceAsync(string zone);
    Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync(string zone);
}
