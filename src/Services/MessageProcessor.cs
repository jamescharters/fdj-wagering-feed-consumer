using System.Text.Json;
using WageringStatsApi.Models.WebSockets;
using WageringStatsApi.Repositories;

namespace WageringStatsApi.Services;

public interface IMessageProcessor
{
    bool ProcessMessage(ReadOnlySpan<byte> jsonBytes);
}

public class MessageProcessor(ILogger<MessageProcessor> logger, IWageringDataRepository wageringDataRepository) : IMessageProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // DEVNOTE: ReadOnlySpan<byte> prevents allocating a new string for each message received. More of a performance optimisation
    // that avoids unnecessary heap allocations. It is my understanding that the Utf8JsonReader works directly with UTF8 bytes
    // which is what we receive from the web socket anyway..
    public bool ProcessMessage(ReadOnlySpan<byte> jsonBytes)
    {
        try
        {
            var reader = new Utf8JsonReader(jsonBytes);
            var message = JsonSerializer.Deserialize<BaseMessage>(ref reader, JsonOptions);

            if (message == null) return true;
            
            logger.LogDebug("Received {MessageType} message.", message.Type);
            
            switch (message.Type)
            {
                case MessageType.Fixture:
                    // DEVNOTE: Seems for the purposes of the Spec, we do not need any data from this
                    return true;
                    
                case MessageType.EndOfFeed: 
                    return false;
                    
                case MessageType.BetPlaced:
                    logger.LogDebug("Handling {MessageType} message (customer {CustomerId}).", message.Type, message.Payload.GetProperty("CustomerId"));
                    HandleBetPlaced(message.Payload);
                    break;
                    
                default:
                    logger.LogError("Message type '{MessageType}' is unsupported, skipping.", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Message processing error!");
        }
        
        return true;
    }

    private void HandleBetPlaced(JsonElement payload)
    {
        var betPayload = payload.Deserialize<BetPlacedPayload>(JsonOptions);
        
        if (betPayload == null) return;
        
        var standToWin = CalculateStandToWin(betPayload.Stake, betPayload.Odds);

        wageringDataRepository.AddPotentialWinning(betPayload.CustomerId, standToWin);
    }

    // DEVNOTE: we could abstract this logic out to some sort of strategy for BetPlaced messages
    // to make this class even cleaner in terms of implementation, but we keep thigns simple for now
    public static decimal CalculateStandToWin(decimal stake, decimal odds)
    {
        var payout = stake * odds;
        return payout - stake;
    }
}
