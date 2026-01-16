namespace WageringStatsApi.Models;

public record CustomerStatResponse
{
    public long CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TotalStandToWin { get; set; }
}