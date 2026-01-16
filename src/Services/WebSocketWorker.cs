using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using WageringFeedConsumer.Models;
using WageringFeedConsumer.Repositories;

namespace WageringFeedConsumer.Services;

public class WebSocketWorker : BackgroundService
{
    private readonly ILogger<WebSocketWorker> _logger;
    private readonly IWageringDataRepository _wageringDataRepository;
    private readonly IMessageProcessor _messageProcessor;
    private readonly WageringFeedConfig _config;
    private readonly ResiliencePipeline _retryPipeline;

    public WebSocketWorker(
        ILogger<WebSocketWorker> logger,
        IWageringDataRepository wageringDataRepository,
        IMessageProcessor messageProcessor,
        IOptions<WageringFeedConfig> wageringFeedConfig)
    {
        _logger = logger;
        _wageringDataRepository = wageringDataRepository;
        _messageProcessor = messageProcessor;
        _config = wageringFeedConfig.Value;

        // DEVNOTE: add some resilience when trying to connect to the WebSocket
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _config.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(30),
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "WebSocket connection failed (attempt {AttemptNumber}/{MaxRetries})...",
                        args.AttemptNumber + 1,
                        _config.MaxRetryAttempts,
                        args.RetryDelay);

                        
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wsUrl = _config.GetFullWebSocketUrl();

        try
        {
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            sessionCts.CancelAfter(TimeSpan.FromMinutes(_config.MaxFeedDurationMinutes));

            _wageringDataRepository.Clear();

            await _retryPipeline.ExecuteAsync(async ct =>
            {
                await ConnectAndProcessAsync(wsUrl, ct);
            }, sessionCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (stoppingToken.IsCancellationRequested)
                _logger.LogInformation("Application stopping.");
            else
                _logger.LogWarning("Session limit reached. Disconnecting.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket connection failed after {MaxRetries} attempts. Giving up.", _config.MaxRetryAttempts);
        }
        finally
        {
            _wageringDataRepository.MarkFeedComplete();
        }
    }

    private async Task ConnectAndProcessAsync(string wsUrl, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting to WebSocket...");
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUrl), cancellationToken);

        _logger.LogInformation("WebSocket Connected.");

        var buffer = new byte[4096];
        using var messageBuffer = new MemoryStream();

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                break;
            }

            // DEVNOTE: we have to be clever here to handle messages that may be split across multiple frames
            // and we assume those that do are not excessively large.

            // 1. Accumulate chunks until we have the complete message
            messageBuffer.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage) continue;

            // 2. Process the complete message
            var messageBytes = messageBuffer.GetBuffer().AsSpan(0, (int)messageBuffer.Length);
            var shouldContinue = _messageProcessor.ProcessMessage(messageBytes);

            // 3. Reset buffer for next message
            messageBuffer.SetLength(0);

            if (shouldContinue) continue;

            _logger.LogInformation("EndOfFeed received. Closing connection.");
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "EndOfFeed", cancellationToken);
            return;
        }
    }
}