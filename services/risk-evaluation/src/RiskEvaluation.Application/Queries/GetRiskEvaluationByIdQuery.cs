using MediatR;

namespace RiskEvaluation.Application.Queries;

public class GetRiskEvaluationByIdQuery : IRequest<RiskEvaluationDto?>
{
    public Guid Id { get; set; }
}