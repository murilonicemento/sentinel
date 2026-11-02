using MediatR;
using RiskEvaluation.Application.DTOs;

namespace RiskEvaluation.Application.Queries;

/// <summary>Pages the evaluations recorded for a location, newest first and then by id.</summary>
public class GetRiskHistoryPageQuery : IRequest<PagedResultDTO<RiskEvaluationDto>>
{
    public int Latitude { get; init; }
    public int Longitude { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
