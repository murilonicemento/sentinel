using MediatR;

namespace RiskEvaluation.Application.Queries;

public class GetRiskHistoryQuery : IRequest<List<RiskEvaluationDto>>
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
}