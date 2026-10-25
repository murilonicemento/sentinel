using MediatR;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Queries;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

public class GetRiskHistoryPageQueryHandler
    : IRequestHandler<GetRiskHistoryPageQuery, PagedResultDTO<RiskEvaluationDto>>
{
    private readonly IRiskEvaluationRepository _repository;

    public GetRiskHistoryPageQueryHandler(IRiskEvaluationRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDTO<RiskEvaluationDto>> Handle(GetRiskHistoryPageQuery request,
        CancellationToken cancellationToken)
    {
        var evaluations = (await _repository.GetByLocationAsync(request.Latitude, request.Longitude))
            .OrderByDescending(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .ToList();

        return new PagedResultDTO<RiskEvaluationDto>
        {
            Items = evaluations
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(e => new RiskEvaluationDto
                {
                    Id = e.Id,
                    Latitude = e.Latitude,
                    Longitude = e.Longitude,
                    Score = e.Score,
                    Level = e.Level.ToString(),
                    Timestamp = e.Timestamp
                })
                .ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = evaluations.Count,
            TotalPages = (int)Math.Ceiling(evaluations.Count / (double)request.PageSize)
        };
    }
}
