using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using WageringStatsApi.Models;
using WageringStatsApi.Repositories;

namespace WageringStatsApi.Services;

public class WebSocketWorker : BackgroundService
{
    private readonly ILogger<WebSocketWorker> _logger;
    private readonly IWageringDataRepository _wageringDataRepository;
    private readonly IMessageProcessor _messageProcessor;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly WageringFeedConfig _config;
    private readonly ResiliencePipeline _retryPipeline;

    public WebSocketWorker(
        ILogger<WebSocketWorker> logger,
        IWageringDataRepository wageringDataRepository,
        IMessageProcessor messageProcessor,
        IHostApplicationLifetime applicationLifetime,
        IOptions<WageringFeedConfig> wageringFeedConfig)
    {
        _logger = logger;
        _wageringDataRepository = wageringDataRepository;
        _messageProcessor = messageProcessor;
        _applicationLifetime = applicationLifetime;
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
                        "WebSocket connection failed (attempt {AttemptNumber}/{MaxRetries}). Retrying in {Delay}...",
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

            _wageringDataRepository.MarkFeedComplete();
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
            _logger.LogError(ex, "WebSocket connection failed after {MaxRetries} attempts. Terminating application due to unrecoverable WebSocket connection failure.", _config.MaxRetryAttempts);

            // DEVNOTE: unrecoverable error if we cannot establish the WebSocket connection - stop the application
            _applicationLifetime.StopApplication();
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

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
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
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "EndOfFeed", CancellationToken.None);
                return;
            }
        }
        catch (OperationCanceledException) when (ws.State == WebSocketState.Open)
        {
            // Try to send a graceful close before disposing
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", closeCts.Token);
            }
            catch
            {
                // Best effort - if close fails, the using statement will abort anyway
            }
            throw;
        }
    }
}