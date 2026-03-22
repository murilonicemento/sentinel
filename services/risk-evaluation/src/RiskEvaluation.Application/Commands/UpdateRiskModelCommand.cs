using MediatR;

namespace RiskEvaluation.Application.Commands;

public class UpdateRiskModelCommand : IRequest<UpdateRiskModelResponse>
{
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
    public string Formula { get; set; } = string.Empty;
}