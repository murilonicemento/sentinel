using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Commands;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Services;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.Handlers;

public class EvaluateRiskCommandHandler : IRequestHandler<EvaluateRiskCommand, EvaluateRiskResponse>
{
    private readonly IRiskEvaluationService _riskEvaluationService;
    private readonly ILogger<EvaluateRiskCommandHandler> _logger;

    public EvaluateRiskCommandHandler(IRiskEvaluationService riskEvaluationService,
        ILogger<EvaluateRiskCommandHandler> logger)
    {
        _riskEvaluationService = riskEvaluationService;
        _logger = logger;
    }

    public async Task<EvaluateRiskResponse> Handle(EvaluateRiskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var evaluation = await _riskEvaluationService.EvaluateRiskAsync(
                request.Latitude,
                request.Longitude,
                request.Metrics,
                request.Events);

            return new EvaluateRiskResponse
            {
                Id = evaluation.Id,
                Latitude = evaluation.Latitude,
                Longitude = evaluation.Longitude,
                Score = evaluation.Score,
                Level = evaluation.Level.ToString(),
                Timestamp = evaluation.Timestamp,
                Factors = new RiskFactors
                {
                    Metrics = request.Metrics,
                    Events = request.Events
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate risk. Latitude: {Latitude}, Longitude: {Longitude}", request.Latitude, request.Longitude);

            throw;
        }
    }
}