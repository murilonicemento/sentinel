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
        _logger.LogInformation("Fetching risk history for location: {Location}", request.Location);

        var evaluations = await _repository.GetByLocationAsync(request.Location);

        _logger.LogInformation("Retrieved {Count} risk evaluations for location: {Location}", evaluations.Count, request.Location);

        return evaluations.Select(e => new RiskEvaluationDto
        {
            Id = e.Id,
            Location = e.Location,
            Score = e.Score,
            Level = e.Level.ToString(),
            Timestamp = e.Timestamp
        }).ToList();
    }
}