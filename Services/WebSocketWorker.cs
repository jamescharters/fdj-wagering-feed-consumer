using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using WageringFeedConsumer.Models;
using WageringFeedConsumer.Repositories;

namespace WageringFeedConsumer.Services;

public class WebSocketWorker(
    ILogger<WebSocketWorker> logger, 
    IWageringDataRepository wageringDataRepository, 
    IMessageProcessor messageProcessor,
    IOptions<WageringFeedConfig> wageringFeedConfig) : BackgroundService
{
    private readonly TimeSpan _maxSessionDuration = TimeSpan.FromMinutes(wageringFeedConfig.Value.MaxFeedDurationMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wsUrl = wageringFeedConfig.Value.GetFullWebSocketUrl();

        try
        {
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            sessionCts.CancelAfter(_maxSessionDuration);

            wageringDataRepository.Clear();

            while (!sessionCts.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation("Connecting to WebSocket...");
                    using var ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri(wsUrl), sessionCts.Token);

                    logger.LogInformation("WebSocket Connected.");

                    // DEVNOTE: buffer size of 4KB should be sufficient for messages based on observed examples
                    var buffer = new byte[4096];

                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), sessionCts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", sessionCts.Token);
                            break;
                        }

                        // DEVNOTE: we pass the buffer as a ReadOnlySpan<byte> to avoid allocating a new string for each message
                        if (messageProcessor.ProcessMessage(buffer.AsSpan(0, result.Count))) continue;

                        logger.LogInformation("EndOfFeed received. Closing connection.");
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "EndOfFeed", sessionCts.Token);
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Handle polite timeout (and disconnection) if we have not received an EndOfFeed message.
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "WebSocket error encountered. Reconnecting in 10 seconds.");
                    await Task.Delay(10000, sessionCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (stoppingToken.IsCancellationRequested)
                logger.LogInformation("Application stopping.");
            else
                logger.LogWarning("Session limit reached. Disconnecting.");
        }
        finally
        {
            wageringDataRepository.MarkFeedComplete();
        }
    }
}