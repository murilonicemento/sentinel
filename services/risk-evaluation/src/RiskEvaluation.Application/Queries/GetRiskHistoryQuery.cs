using MediatR;

namespace RiskEvaluation.Application.Queries;

public class GetRiskHistoryQuery : IRequest<List<RiskEvaluationDto>>
{
    public string Location { get; set; } = string.Empty;
}