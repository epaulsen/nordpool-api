namespace NordpoolApi.Services;

public interface INordpoolApiClient
{
    /// <summary>
    /// Fetches electricity price data from Nordpool API for a specific date.
    /// </summary>
    /// <param name="date">The date to fetch data for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing the price data, or null if no data is available (HTTP 204)</returns>
    Task<string?> FetchPriceDataAsync(DateOnly date, CancellationToken cancellationToken = default);
}
