using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WageringFeedConsumer.Controllers;
using WageringFeedConsumer.Models;
using WageringFeedConsumer.Repositories;
using WageringFeedConsumer.Services;

namespace WageringFeedConsumer.Tests.Controllers;

[TestFixture]
public class CustomerControllerTests
{
    private Mock<IWageringDataRepository> _repositoryMock = null!;
    private Mock<ICustomerService> _customerServiceMock = null!;
    private CustomerController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IWageringDataRepository>();
        _customerServiceMock = new Mock<ICustomerService>();
        _controller = new CustomerController(_repositoryMock.Object, _customerServiceMock.Object);

        // Default: feed is not complete
        _repositoryMock.Setup(x => x.IsFeedComplete).Returns(false);

        // Default: return a customer with name for any lookup
        _customerServiceMock
            .Setup(x => x.GetCustomerAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => new CustomerInfo(id, "Test Customer"));
    }

    [Test]
    public async Task GetCustomerStats_ValidCustomerWithData_ReturnsOkWithStats()
    {
        const long customerId = 123;
        const decimal standToWin = 150.567m;

        _repositoryMock.Setup(x => x.GetTotalStandToWin(customerId)).Returns(standToWin);

        var result = await _controller.GetCustomerStats(customerId, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());

        var okResult = (OkObjectResult)result;
        var response = okResult.Value as CustomerStatResponse;
        
        Assert.Multiple(() =>
        {
            Assert.That(response!.CustomerId, Is.EqualTo(customerId));
            Assert.That(response.TotalStandToWin, Is.EqualTo(150.57m)); // rounded to 2 decimal places
            Assert.That(response.Name, Is.EqualTo("Test Customer"));
        });
    }


    [Test]
    public async Task GetCustomerStats_CustomerExistsButNoBets_ReturnsOkWithZero()
    {
        const long customerId = 999;
        _repositoryMock.Setup(x => x.GetTotalStandToWin(customerId)).Returns((decimal?)null);
        
        var result = await _controller.GetCustomerStats(customerId, CancellationToken.None);
        
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        var response = okResult.Value as CustomerStatResponse;
        
        Assert.Multiple(() =>
        {
            Assert.That(response!.CustomerId, Is.EqualTo(customerId));
            Assert.That(response.TotalStandToWin, Is.EqualTo(0m));
            Assert.That(response.Name, Is.EqualTo("Test Customer"));
        });
    }

    [Test]
    public async Task GetCustomerStats_InvalidCustomerId_ReturnsBadRequest()
    {
        const long invalidCustomerId = 0;
        var result = await _controller.GetCustomerStats(invalidCustomerId, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }

    [Test]
    public async Task GetCustomerStats_NegativeCustomerId_ReturnsBadRequest()
    {
        const long negativeCustomerId = -1;

        var result = await _controller.GetCustomerStats(negativeCustomerId, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }

    [Test]
    public async Task GetCustomerStats_FeedComplete_ReturnsServiceUnavailable()
    {
        // DEVNOTE: There is an assumption that once the feed is marked complete,
        // no further data will be added, so any request for stats after that point
        // should simply return 503 Service Unavailable...

        const long customerId = 123;
        _repositoryMock.Setup(x => x.IsFeedComplete).Returns(true);

        var result = await _controller.GetCustomerStats(customerId, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<StatusCodeResult>());
        var statusCodeResult = (StatusCodeResult)result;
        Assert.That(statusCodeResult.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
    }

    [Test]
    public async Task GetCustomerStats_MultipleWinnings_ReturnsAccumulatedTotal()
    {
        const long customerId = 123;
        _repositoryMock.Setup(x => x.GetTotalStandToWin(customerId)).Returns(150.00m);

        var result = await _controller.GetCustomerStats(customerId, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        var response = okResult.Value as CustomerStatResponse;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.TotalStandToWin, Is.EqualTo(150.00m));
        Assert.That(response!.Name, Is.EqualTo("Test Customer"));
    }

    [Test]
    public async Task GetCustomerStats_CustomerServiceReturnsNull_ReturnsNotFound()
    {
        const long customerId = 123;
        _repositoryMock.Setup(x => x.GetTotalStandToWin(customerId)).Returns(100m);
        
        _customerServiceMock
            .Setup(x => x.GetCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerInfo?)null);

        var result = await _controller.GetCustomerStats(customerId, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }
}
