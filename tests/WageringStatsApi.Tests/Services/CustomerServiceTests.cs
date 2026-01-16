using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using WageringStatsApi.Models;
using WageringStatsApi.Repositories;
using WageringStatsApi.Services;

namespace WageringStatsApi.Tests.Services;

[TestFixture]
public class CustomerServiceTests
{
    private Mock<ICustomerRepository> _repositoryMock = null!;
    private Mock<ILogger<CustomerService>> _loggerMock = null!;
    private Mock<HttpMessageHandler> _httpHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private CustomerService _service = null!;


    private static readonly WageringFeedConfig TestConfig = new()
    {
        CandidateId = "some-candidate-id",
        CustomerApiUrl = "http://my-test-api.com.au/customer",
        WebSocketUrl = "ws://my-test-api.com.au/ws",
        MaxFeedDurationMinutes = 5
    };

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<ICustomerRepository>();
        _loggerMock = new Mock<ILogger<CustomerService>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);

        var options = Options.Create(TestConfig);
        _service = new CustomerService(_httpClient, _repositoryMock.Object, options, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    [Test]
    public async Task GetCustomerAsync_CachedCustomer_ReturnsFromRepository()
    {
        const long customerId = 123;
        var cachedCustomer = new CustomerInfo(customerId, "Cached Customer");

        _repositoryMock
            .Setup(x => x.TryGet(customerId, out cachedCustomer))
            .Returns(true);

        var result = await _service.GetCustomerAsync(customerId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CustomerName, Is.EqualTo("Cached Customer"));

        // underlying HTTP should never be called
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task GetCustomerAsync_NotCached_FetchesFromApiAndCaches()
    {
        const long customerId = 123;
        CustomerInfo? nullCustomer = null;
        var apiResponse = new CustomerInfo(customerId, "API Customer");

        _repositoryMock
            .Setup(x => x.TryGet(customerId, out nullCustomer))
            .Returns(false);

        SetupHttpResponse(HttpStatusCode.OK, apiResponse);


        var result = await _service.GetCustomerAsync(customerId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CustomerName, Is.EqualTo("API Customer"));

        _repositoryMock.Verify(x => x.Add(customerId, It.Is<CustomerInfo>(c => c.CustomerName == "API Customer")), Times.Once);
    }

    
    private void SetupHttpResponse(HttpStatusCode statusCode, CustomerInfo? content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = content is not null
                ? JsonContent.Create(content)
                : new StringContent("null", System.Text.Encoding.UTF8, "application/json")
        };

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

}
