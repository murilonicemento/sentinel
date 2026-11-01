namespace RiskEvaluation.Domain.Interfaces;

/// <summary>Recent scores by exact position: a nearby point does not reuse another point's score (RE-18).</summary>
public interface IRecentScoresCache
{
    Task<RiskScoreCacheEntry?> GetAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task SetAsync(double latitude, double longitude, RiskScoreCacheEntry entry, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>Removes every cached score; returns how many entries were removed.</summary>
    Task<long> InvalidateAllAsync(CancellationToken cancellationToken = default);
}