using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using WageringFeedConsumer.Models.WebSockets;
using WageringFeedConsumer.Repositories;
using WageringFeedConsumer.Services;

namespace WageringFeedConsumer.Tests.Services;

[TestFixture]
public class MessageProcessorTests
{
    private Mock<ILogger<MessageProcessor>> _loggerMock = null!;
    private Mock<IWageringDataRepository> _repositoryMock = null!;
    private MessageProcessor _processor = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<MessageProcessor>>();
        _repositoryMock = new Mock<IWageringDataRepository>();
        _processor = new MessageProcessor(_loggerMock.Object, _repositoryMock.Object);
    }

    [Test]
    public void ProcessMessage_BetPlacedMessage_AddsWinningToRepository()
    {
        var message = CreateBetPlacedMessage(customerId: 123, stake: 10m, odds: 2.5m);
        var result = _processor.ProcessMessage(message);
        
        Assert.That(result, Is.True); // should continue processing
        _repositoryMock.Verify(x => x.AddPotentialWinning(123, 15m), Times.Once);
    }

    [Test]
    public void ProcessMessage_EndOfFeedMessage_ReturnsFalse()
    {
        var message = CreateMessage(MessageType.EndOfFeed);
        var result = _processor.ProcessMessage(message);
        Assert.That(result, Is.False);
    }

    [Test]
    public void ProcessMessage_FixtureMessage_ReturnsTrueAndDoesNothing()
    {
        // DEVNOTE: fixture messages do not impact winnings, they are ignored
        var message = CreateMessage(MessageType.Fixture);
        var result = _processor.ProcessMessage(message);
        Assert.That(result, Is.True);
        _repositoryMock.Verify(x => x.AddPotentialWinning(It.IsAny<long>(), It.IsAny<decimal>()), Times.Never);
    }

    [Test]
    public void ProcessMessage_MultipleBetPlacedMessages_CallsAddPotentialWinningForEach()
    {
        var bp1 = CreateBetPlacedMessage(customerId: 1, stake: 10m, odds: 2.0m); 
        var bp2 = CreateBetPlacedMessage(customerId: 1, stake: 20m, odds: 1.5m);
        var bp3 = CreateBetPlacedMessage(customerId: 1, stake: 5m, odds: 3.0m);

        _processor.ProcessMessage(bp1);
        _processor.ProcessMessage(bp2);
        _processor.ProcessMessage(bp3);

        // All three happen to produce the same stand-to-win value of 10
        _repositoryMock.Verify(x => x.AddPotentialWinning(1, 10m), Times.Exactly(3));
    }

    [Test]
    public void ProcessMessage_InvalidJson_ReturnsTrueAndContinues()
    {
        var invalidJson = Encoding.UTF8.GetBytes("{ something broken }}}");
        var result = _processor.ProcessMessage(invalidJson);

        Assert.That(result, Is.True);
    }

    [Test]
    public void ProcessMessage_EmptyJson_ReturnsTrueAndContinues()
    {
        var emptyJson = Encoding.UTF8.GetBytes("{}");
        var result = _processor.ProcessMessage(emptyJson);
        Assert.That(result, Is.True);
    }

    [Test]
    public void CalculateStandToWin_ValidInputs_ReturnsCorrectValue()
    {
        // DEVNOTE: just some general sanity checks on calculations
        Assert.Multiple(() =>
        {
            Assert.That(MessageProcessor.CalculateStandToWin(10m, 2.0m), Is.EqualTo(10m));
            Assert.That(MessageProcessor.CalculateStandToWin(100m, 1.5m), Is.EqualTo(50m));
            Assert.That(MessageProcessor.CalculateStandToWin(50m, 3.0m), Is.EqualTo(100m));
            Assert.That(MessageProcessor.CalculateStandToWin(25m, 1.0m), Is.EqualTo(0m));
        });
    }

    [Test]
    public void ProcessMessage_DifferentCustomers_TracksIndependently()
    {
        var bp1 = CreateBetPlacedMessage(customerId: 1, stake: 10m, odds: 2.0m);
        var bp2 = CreateBetPlacedMessage(customerId: 2, stake: 20m, odds: 3.0m);

        _processor.ProcessMessage(bp1);
        _processor.ProcessMessage(bp2);

        _repositoryMock.Verify(x => x.AddPotentialWinning(1, 10m), Times.Once);
        _repositoryMock.Verify(x => x.AddPotentialWinning(2, 40m), Times.Once);
    }


    private static byte[] CreateBetPlacedMessage(long customerId, decimal stake, decimal odds)
    {
        var payload = new BetPlacedPayload
        {
            CustomerId = customerId,
            FixtureId = 1,
            OutcomeKey = "0a4dc8f", // these vary but are not relevant for the test
            Stake = stake,
            Odds = odds
        };

        var message = new
        {
            Type = "BetPlaced",
            Payload = payload,
            Timestamp = DateTime.UtcNow
        };

        return JsonSerializer.SerializeToUtf8Bytes(message);
    }

    private static byte[] CreateMessage(MessageType type)
    {
        var message = new
        {
            Type = type.ToString(),
            Payload = new { },
            Timestamp = DateTime.UtcNow
        };

        return JsonSerializer.SerializeToUtf8Bytes(message);
    }
}
