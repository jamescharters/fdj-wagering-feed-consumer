using System.Collections.Concurrent;
using WageringStatsApi.Models;

namespace WageringStatsApi.Repositories;

public interface ICustomerRepository
{
    bool TryGet(long customerId, out CustomerInfo? customer);
    void Add(long customerId, CustomerInfo customer);
}

public class CustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<long, CustomerInfo> _customers = new();

    public bool TryGet(long customerId, out CustomerInfo? customer)
    {
        return _customers.TryGetValue(customerId, out customer);
    }

    public void Add(long customerId, CustomerInfo customer)
    {
        _customers.TryAdd(customerId, customer);
    }
}
