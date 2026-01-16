using WageringStatsApi.Models;
using WageringStatsApi.Repositories;

namespace WageringStatsApi.Tests.Repositories;

[TestFixture]
public class CustomerRepositoryTests
{
    private CustomerRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new CustomerRepository();
    }

    [Test]
    public void TryGet_NonExistentCustomer_ReturnsFalse()
    {
        var result = _repository.TryGet(123, out var customer);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(customer, Is.Null);
        });
    }

    [Test]
    public void Add_NewCustomer_CanBeRetrieved()
    {
        var customerInfo = new CustomerInfo(123, "James Charters");

        _repository.Add(123, customerInfo);
        var result = _repository.TryGet(123, out var retrieved);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.Id, Is.EqualTo(123));
            Assert.That(retrieved.CustomerName, Is.EqualTo("James Charters"));
        });
    }

    [Test]
    public void Add_DuplicateCustomer_DoesNotOverwrite()
    {
        var original = new CustomerInfo(123, "Original Name");
        var duplicate = new CustomerInfo(123, "New Name");

        _repository.Add(123, original);
        _repository.Add(123, duplicate);

        _repository.TryGet(123, out var retrieved);

        Assert.That(retrieved!.CustomerName, Is.EqualTo("Original Name"));
    }

    [Test]
    public void Add_MultipleCustomers_TracksIndependently()
    {
        var customer1 = new CustomerInfo(1, "James");
        var customer2 = new CustomerInfo(2, "Marius");
        var customer3 = new CustomerInfo(3, "Joe");

        _repository.Add(1, customer1);
        _repository.Add(2, customer2);
        _repository.Add(3, customer3);

        _repository.TryGet(1, out var retrieved1);
        _repository.TryGet(2, out var retrieved2);
        _repository.TryGet(3, out var retrieved3);

        Assert.Multiple(() =>
        {
            Assert.That(retrieved1!.CustomerName, Is.EqualTo("James"));
            Assert.That(retrieved2!.CustomerName, Is.EqualTo("Marius"));
            Assert.That(retrieved3!.CustomerName, Is.EqualTo("Joe"));
        });
    }

    [Test]
    public void Add_ConcurrentAccess_ThreadSafe()
    {
        const int threadCount = 100;
        var tasks = new Task[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var customerId = i;
            tasks[i] = Task.Run(() =>
            {
                _repository.Add(customerId, new CustomerInfo(customerId, $"Customer {customerId}"));
            });
        }

        Task.WaitAll(tasks);

        // Verify all customers were added
        for (var i = 0; i < threadCount; i++)
        {
            var found = _repository.TryGet(i, out var customer);
            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True, $"Customer {i} should exist");
                Assert.That(customer!.Id, Is.EqualTo(i));
            });
        }
    }
}
