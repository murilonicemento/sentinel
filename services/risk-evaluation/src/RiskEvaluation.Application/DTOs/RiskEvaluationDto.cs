namespace RiskEvaluation.Application.DTOs;

public class RiskEvaluationDto
{
    public Guid Id { get; set; }
    public string Location { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}