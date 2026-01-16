using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using WageringStatsApi.Models;

namespace WageringStatsApi.Services;

public interface ICustomerService
{
    Task<CustomerInfo?> GetCustomerAsync(long customerId, CancellationToken cancellationToken = default);
}

public class CustomerService : ICustomerService
{
    private readonly HttpClient _httpClient;
    private readonly WageringFeedConfig _config;
    private readonly ILogger<CustomerService> _logger;

    // Simple in-memory cache to avoid repeated calls for same customer. Threadsafe.
    private readonly ConcurrentDictionary<long, CustomerInfo> _cache = new();

    public CustomerService(HttpClient httpClient, IOptions<WageringFeedConfig> config,
        ILogger<CustomerService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<CustomerInfo?> GetCustomerAsync(long customerId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(customerId, out var cached))
        {
            return cached;
        }

        try
        {
            // DEVNOTE: ideally this URL construction would be handled by a UriBuilder or similar, but keeping simple here
            // Also, we would want perhaps an out-of-process caching solution for a real-world service

            var url = $"{_config.CustomerApiUrl}?customerId={customerId}&candidateId={Uri.EscapeDataString(_config.CandidateId)}";
            var response = await _httpClient.GetFromJsonAsync<CustomerInfo>(url, cancellationToken);

            if (response is not null)
            {
                _cache.TryAdd(customerId, response);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch customer {CustomerId} from API", customerId);
            return null;
        }
    }
}
