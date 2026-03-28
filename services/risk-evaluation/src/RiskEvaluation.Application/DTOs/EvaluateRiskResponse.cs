namespace RiskEvaluation.Application.DTOs;

public class EvaluateRiskResponse
{
    public Guid Id { get; set; }
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public RiskFactors Factors { get; set; } = new();
}