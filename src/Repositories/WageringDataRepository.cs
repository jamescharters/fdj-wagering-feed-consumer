using System.Collections.Concurrent;

namespace WageringStatsApi.Repositories;

public interface IWageringDataRepository
{
    bool IsFeedComplete { get; }
    void AddPotentialWinning(long customerId, decimal amount);
    decimal? GetTotalStandToWin(long customerId);
    void MarkFeedComplete();
    void Clear();
}

public class WageringDataRepository(ILogger<WageringDataRepository> logger) : IWageringDataRepository
{
    // DEVNOTE: Very naive implementation but threadsafe. Key is CustomerId, Value is StandToWin amount.
    
    // A potential extension of this would to have (and lock) a data structure if we need more than just that
    // StandToWin amount
    
    // It goes without saying that an ephemeral in-memory approach like this has drawbacks in case of service restart,
    // of if there are multiple instances of this API running (i.e. congruence of data used by APIs to respond to 
    // requests) and so on.
    private readonly ConcurrentDictionary<long, decimal> _wageringData = new();
    private volatile bool _isFeedComplete;

    public bool IsFeedComplete => _isFeedComplete;
    
    public void AddPotentialWinning(long customerId, decimal amount)
    {
        _wageringData.AddOrUpdate(customerId, amount, 
            (_, existingVal) => existingVal + amount);
    }

    public decimal? GetTotalStandToWin(long customerId)
    {
        return _wageringData.TryGetValue(customerId, out var total) ? total : null;
    }
    
    public void MarkFeedComplete()
    {
        _isFeedComplete = true;
        logger.LogInformation("Feed marked as complete");
    }

    public void Clear()
    {
        // DEVNOTE: there is an assumption that Clear() is never called while other operations are ongoing
        // otherwise we would need to lock the structure for thread safety
        
        _wageringData.Clear();
        _isFeedComplete = false;
    }
}