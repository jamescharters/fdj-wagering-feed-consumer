namespace WageringStatsApi.Models.WebSockets;

public record BetPlacedPayload
{
    public long CustomerId { get; init; }
    public long FixtureId { get; init; }
    public string OutcomeKey { get; init; } = string.Empty;
    public decimal Odds { get; init; }
    public decimal Stake { get; init; }
}