using NordpoolApi.Models;

namespace NordpoolApi.Services;

public interface IPriceService
{
    Task<IEnumerable<ElectricityPrice>> GetCurrentPricesAsync();
    Task<ElectricityPrice?> GetCurrentPriceAsync();
    Task<IEnumerable<ElectricityPrice>> GetAllPricesSortedAsync();
}
