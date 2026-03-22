using MediatR;

namespace RiskEvaluation.Application.Commands;

public class EvaluateRiskCommand : IRequest<EvaluateRiskResponse>
{
    public string Location { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
}