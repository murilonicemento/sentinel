using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Queries;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

public class GetRiskEvaluationByIdQueryHandler : IRequestHandler<GetRiskEvaluationByIdQuery, RiskEvaluationDto?>
{
    private readonly IRiskEvaluationRepository _repository;
    private readonly ILogger<GetRiskEvaluationByIdQueryHandler> _logger;

    public GetRiskEvaluationByIdQueryHandler(IRiskEvaluationRepository repository, ILogger<GetRiskEvaluationByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RiskEvaluationDto?> Handle(GetRiskEvaluationByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching risk evaluation by ID: {Id}", request.Id);

        var evaluation = await _repository.GetByIdAsync(request.Id);
        if (evaluation == null)
        {
            _logger.LogWarning("Risk evaluation not found for ID: {Id}", request.Id);
            return null;
        }

        _logger.LogInformation("Found risk evaluation for ID: {Id}, Location: {Location}", request.Id, evaluation.Location);

        return new RiskEvaluationDto
        {
            Id = evaluation.Id,
            Location = evaluation.Location,
            Score = evaluation.Score,
            Level = evaluation.Level.ToString(),
            Timestamp = evaluation.Timestamp
        };
    }
}