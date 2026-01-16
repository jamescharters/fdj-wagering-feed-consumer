using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using WageringStatsApi.Models;
using WageringStatsApi.Repositories;

namespace WageringStatsApi.Services;

public interface ICustomerService
{
    Task<CustomerInfo?> GetCustomerAsync(long customerId, CancellationToken cancellationToken = default);
}

public class CustomerService(
    HttpClient httpClient,
    ICustomerRepository customerRepository,
    IOptions<WageringFeedConfig> config,
    ILogger<CustomerService> logger)
    : ICustomerService
{
    private readonly WageringFeedConfig _config = config.Value;

    public async Task<CustomerInfo?> GetCustomerAsync(long customerId, CancellationToken cancellationToken = default)
    {
        if (customerRepository.TryGet(customerId, out var cached))
        {
            return cached;
        }

        try
        {
            logger.LogDebug("Calling Customer API for {CustomerId}.", customerId);

            var url = $"{_config.CustomerApiUrl}?customerId={customerId}&candidateId={Uri.EscapeDataString(_config.CandidateId)}";
            var response = await httpClient.GetFromJsonAsync<CustomerInfo>(url, cancellationToken);

            if (response is not null)
            {
                customerRepository.Add(customerId, response);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch customer {CustomerId} from API", customerId);
            return null;
        }
    }
}
