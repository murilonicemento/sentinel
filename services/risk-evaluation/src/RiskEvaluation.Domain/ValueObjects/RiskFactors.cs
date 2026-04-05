using System.Text.Json.Serialization;

namespace RiskEvaluation.Domain.ValueObjects;

/// <summary>
/// Unified risk factors container with both continuous metrics and discrete events.
/// </summary>
public class RiskFactors
{
    [JsonPropertyName("metrics")]
    public RiskMetrics Metrics { get; set; } = new();

    [JsonPropertyName("events")]
    public RiskEvents Events { get; set; } = new();
}
