namespace WageringFeedConsumer.Models;

public class WageringFeedConfig
{
    public const string SectionName = "WageringFeed";
    
    public string CandidateId { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public string CustomerApiUrl { get; set; } = string.Empty;
    public int MaxFeedDurationMinutes { get; set; }

    // DEVNOTE: Something like FluentValidation could be used here, but below is just done for simplicity's sake
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId))
        {
            throw new ArgumentNullException($"{nameof(CandidateId)} must be provided");
        }

        if (string.IsNullOrWhiteSpace(WebSocketUrl))
        {
            throw new ArgumentNullException(
                $"{nameof(WebSocketUrl)} is required in WageringFeed configuration");
        }

        if (!Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            throw new ArgumentOutOfRangeException(
                $"Invalid WebSocketUrl: {WebSocketUrl}. Must be a valid ws:// or wss:// URL");
        }

        if (MaxFeedDurationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                $"{nameof(MaxFeedDurationMinutes)} must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(CustomerApiUrl))
        {
            throw new ArgumentNullException(
                $"{nameof(CustomerApiUrl)} is required in WageringFeed configuration");
        }

        if (!Uri.TryCreate(CustomerApiUrl, UriKind.Absolute, out var customerUri) || 
            (customerUri.Scheme != "http" && customerUri.Scheme != "https"))
        {
            throw new ArgumentOutOfRangeException(
                $"Invalid CustomerApiUrl: {CustomerApiUrl}. Must be a valid http:// or https:// URL");
        }
    }

    public string GetFullWebSocketUrl()
    {
        return $"{WebSocketUrl}?candidateId={Uri.EscapeDataString(CandidateId)}";
    }
}