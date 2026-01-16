using System.Buffers;
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

                    var buffer = new byte[4096];
                    using var messageBuffer = new MemoryStream();

                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), sessionCts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", sessionCts.Token);
                            break;
                        }

                        // DEVNOTE: we have to be clever here to handle messages that may be split across multiple frames
                        // and we assume those that do are not excessively large.

                        // 1. Accumulate chunks until we have the complete message
                        messageBuffer.Write(buffer, 0, result.Count);
                        
                        if (!result.EndOfMessage) continue;
                        
                        // 2. Process the complete message
                        var messageBytes = messageBuffer.GetBuffer().AsSpan(0, (int)messageBuffer.Length);
                        var shouldContinue = messageProcessor.ProcessMessage(messageBytes);
                        
                        // 3. Reset buffer for next message
                        messageBuffer.SetLength(0);
                        
                        if (shouldContinue) continue;

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