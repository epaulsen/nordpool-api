using NordpoolApi.Models;

namespace NordpoolApi.Services;

public interface IPriceService
{
    Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync();
    Task<ElectricityPrice?> GetCurrentPriceAsync();
    Task<IEnumerable<ElectricityPrice>> GetAllPricesSortedAsync();
    Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync(string zone);
    Task<IEnumerable<ElectricityPrice>> GetAllPricesSortedAsync(string zone);
}
