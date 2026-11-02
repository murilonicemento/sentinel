namespace RiskEvaluation.Application.DTOs;

public class UpdateRiskModelResponse
{
    public bool Success { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}