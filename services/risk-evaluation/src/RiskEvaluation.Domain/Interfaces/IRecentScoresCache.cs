namespace RiskEvaluation.Domain.Interfaces;

public interface IRecentScoresCache
{
    Task<RiskScoreCacheEntry?> GetAsync(int latitude, int longitude, CancellationToken cancellationToken = default);
    Task SetAsync(int latitude, int longitude, RiskScoreCacheEntry entry, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}