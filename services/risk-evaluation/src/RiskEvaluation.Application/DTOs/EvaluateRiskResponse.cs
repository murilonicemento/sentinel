namespace RiskEvaluation.Application.DTOs;

public class EvaluateRiskResponse
{
    public Guid Id { get; set; }
    public string Location { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> Factors { get; set; } = new();
}