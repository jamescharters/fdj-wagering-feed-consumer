using Microsoft.Extensions.Logging;
using Moq;
using WageringFeedConsumer.Repositories;

namespace WageringFeedConsumer.Tests.Repositories;

[TestFixture]
public class WageringDataRepositoryTests
{
    private Mock<ILogger<WageringDataRepository>> _loggerMock = null!;
    private WageringDataRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<WageringDataRepository>>();
        _repository = new WageringDataRepository(_loggerMock.Object);
    }

    [Test]
    public void AddPotentialWinning_NewCustomer_StoresAmount()
    {
        const long customerId = 123;
        const decimal amount = 100.50m;

        _repository.AddPotentialWinning(customerId, amount);

        var result = _repository.GetTotalStandToWin(customerId);
        Assert.That(result, Is.EqualTo(amount));
    }

    [Test]
    public void AddPotentialWinning_ExistingCustomer_AccumulatesAmount()
    {
        const long customerId = 123;
        const decimal firstAmount = 100.00m;
        const decimal secondAmount = 50.25m;

        _repository.AddPotentialWinning(customerId, firstAmount);
        _repository.AddPotentialWinning(customerId, secondAmount);

        var result = _repository.GetTotalStandToWin(customerId);
        Assert.That(result, Is.EqualTo(firstAmount + secondAmount));
    }

    [Test]
    public void GetTotalStandToWin_NonExistentCustomer_ReturnsNull()
    {
        const long customerId = 123;
        var result = _repository.GetTotalStandToWin(customerId);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void IsFeedComplete_InitialState_ReturnsFalse()
    {
        Assert.That(_repository.IsFeedComplete, Is.False);
    }

    [Test]
    public void MarkFeedComplete_SetsIsFeedCompleteToTrue()
    {
        _repository.MarkFeedComplete();
        Assert.That(_repository.IsFeedComplete, Is.True);
    }

    [Test]
    public void Clear_RemovesAllDataAndResetsFeedComplete()
    {
        _repository.AddPotentialWinning(123, 100m);
        _repository.AddPotentialWinning(456, 200m);
        _repository.MarkFeedComplete();

        _repository.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_repository.GetTotalStandToWin(123), Is.Null);
            Assert.That(_repository.GetTotalStandToWin(456), Is.Null);
            Assert.That(_repository.IsFeedComplete, Is.False);
        });
    }

    [Test]
    public void AddPotentialWinning_MultipleCustomers_TracksIndependently()
    {
        const long customer1 = 100; 
        const long customer2 = 200;
        const decimal amount1 = 50.00m;
        const decimal amount2 = 75.00m;

        _repository.AddPotentialWinning(customer1, amount1);
        _repository.AddPotentialWinning(customer2, amount2);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.GetTotalStandToWin(customer1), Is.EqualTo(amount1));
            Assert.That(_repository.GetTotalStandToWin(customer2), Is.EqualTo(amount2));
        });
    }

    [Test]
    public void AddPotentialWinning_ConcurrentAccess_ThreadSafe()
    {
        const long customerId = 1;
        const int numIterations = 1000;
        const decimal amount = 1.00m;

        Parallel.For(0, numIterations, _ =>
        {
            _repository.AddPotentialWinning(customerId, amount);
        });

        var result = _repository.GetTotalStandToWin(customerId);
        Assert.That(result, Is.EqualTo(numIterations * amount));
    }
}
