using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Domain.Interfaces;

public class RiskScoreCacheEntry
{
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public RiskMetrics Metrics { get; set; } = new();
    public RiskEvents Events { get; set; } = new();
}