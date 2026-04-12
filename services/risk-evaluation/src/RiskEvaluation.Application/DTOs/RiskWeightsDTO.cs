namespace RiskEvaluation.Application.DTOs;

public class RiskWeightsDTO
{
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, double> MetricWeights { get; set; } = new();
    public Dictionary<string, double> EventWeights { get; set; } = new();
    public DateTime EffectiveDate { get; set; }
}
