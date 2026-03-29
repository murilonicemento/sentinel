using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Domain.Interfaces;

public interface IRecentScoresCache
{
    Task<RiskScoreCacheEntry?> GetAsync(int latitude, int longitude, CancellationToken cancellationToken = default);
    Task SetAsync(int latitude, int longitude, RiskScoreCacheEntry entry, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}

public class RiskScoreCacheEntry
{
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public RiskMetrics Metrics { get; set; } = new();
    public RiskEvents Events { get; set; } = new();
}
