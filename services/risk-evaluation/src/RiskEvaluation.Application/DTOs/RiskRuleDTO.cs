namespace RiskEvaluation.Application.DTOs;

public class RiskRuleDTO
{
    public string Condition { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string Action { get; set; } = string.Empty;
}
