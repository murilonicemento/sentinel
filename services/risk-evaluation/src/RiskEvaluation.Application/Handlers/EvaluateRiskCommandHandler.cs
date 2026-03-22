using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Commands;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Interfaces;

namespace RiskEvaluation.Application.Handlers;

public class EvaluateRiskCommandHandler : IRequestHandler<EvaluateRiskCommand, EvaluateRiskResponse>
{
    private readonly IRiskEvaluationService _riskEvaluationService;
    private readonly ILogger<EvaluateRiskCommandHandler> _logger;

    public EvaluateRiskCommandHandler(IRiskEvaluationService riskEvaluationService, ILogger<EvaluateRiskCommandHandler> logger)
    {
        _riskEvaluationService = riskEvaluationService;
        _logger = logger;
    }

    public async Task<EvaluateRiskResponse> Handle(EvaluateRiskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing risk evaluation for location: {Location}", request.Location);

        // Extract metrics
        var gust = request.Metrics.GetValueOrDefault("gust", 0);
        var precipitation = request.Metrics.GetValueOrDefault("precipitation", 0);
        var pressure = request.Metrics.GetValueOrDefault("pressure", 0);

        _logger.LogDebug("Metrics for {Location} - Gust: {Gust}, Precipitation: {Precipitation}, Pressure: {Pressure}",
            request.Location, gust, precipitation, pressure);

        var evaluation = await _riskEvaluationService.EvaluateRiskAsync(request.Location, gust, precipitation, pressure);

        _logger.LogInformation("Risk evaluation completed for {Location} - Score: {Score}, Level: {Level}",
            evaluation.Location, evaluation.Score, evaluation.Level);

        return new EvaluateRiskResponse
        {
            Id = evaluation.Id,
            Location = evaluation.Location,
            Score = evaluation.Score,
            Level = evaluation.Level.ToString(),
            Timestamp = evaluation.Timestamp,
            Factors = new Dictionary<string, double>
            {
                { "gust", gust },
                { "precipitation", precipitation },
                { "pressure", pressure }
            }
        };
    }
}