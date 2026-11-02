using MediatR;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.Commands;

public class EvaluateRiskCommand : IRequest<EvaluateRiskResponse>
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public RiskMetrics Metrics { get; set; } = new();
    public RiskEvents Events { get; set; } = new();
}