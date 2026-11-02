using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Queries;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

public class GetRiskHistoryQueryHandler : IRequestHandler<GetRiskHistoryQuery, List<RiskEvaluationDto>>
{
    private readonly IRiskEvaluationRepository _repository;
    private readonly ILogger<GetRiskHistoryQueryHandler> _logger;

    public GetRiskHistoryQueryHandler(IRiskEvaluationRepository repository, ILogger<GetRiskHistoryQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<RiskEvaluationDto>> Handle(GetRiskHistoryQuery request, CancellationToken cancellationToken)
    {
        var evaluations = await _repository.GetByLocationAsync(request.Latitude, request.Longitude);

        if (evaluations.Count == 0)
        {
            _logger.LogWarning("No risk history found for {Latitude},{Longitude}", request.Latitude, request.Longitude);
        }

        return evaluations.Select(e => new RiskEvaluationDto
        {
            Id = e.Id,
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            Score = e.Score,
            Level = e.Level.ToString(),
            Timestamp = e.Timestamp
        }).ToList();
    }
}