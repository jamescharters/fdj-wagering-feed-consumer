namespace WageringStatsApi.Models;

public class WageringFeedConfig
{
    public const string SectionName = "WageringFeed";

    public string CandidateId { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public string CustomerApiUrl { get; set; } = string.Empty;
    public int MaxFeedDurationMinutes { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;

    public string GetFullWebSocketUrl() => $"{WebSocketUrl}?candidateId={Uri.EscapeDataString(CandidateId)}";
}