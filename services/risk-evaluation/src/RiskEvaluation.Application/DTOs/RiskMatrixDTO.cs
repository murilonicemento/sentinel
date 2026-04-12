using RiskEvaluation.Application.Interfaces.HttpClients;

namespace RiskEvaluation.Application.DTOs;

public class RiskMatrixDTO
{
    public string EventTypeCode { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public int Version { get; set; }
    public Dictionary<string, double> Weights { get; set; } = new();
    public List<RiskRuleDTO> Rules { get; set; } = new();
}
