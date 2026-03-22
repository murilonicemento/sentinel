using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces;

namespace RiskEvaluation.Application.Handlers;

public class SensorEventDetectedHandler : INotificationHandler<SensorEventDetected>
{
    private readonly IRiskEvaluationService _riskEvaluationService;
    private readonly ILogger<SensorEventDetectedHandler> _logger;

    public SensorEventDetectedHandler(IRiskEvaluationService riskEvaluationService, ILogger<SensorEventDetectedHandler> logger)
    {
        _riskEvaluationService = riskEvaluationService;
        _logger = logger;
    }

    public async Task Handle(SensorEventDetected notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing sensor event for location: {Location}, Gust: {Gust}, Precipitation: {Precipitation}, Pressure: {Pressure}",
            notification.Location, notification.Gust, notification.Precipitation, notification.Pressure);

        await _riskEvaluationService.EvaluateRiskAsync(
            notification.Location,
            notification.Gust,
            notification.Precipitation,
            notification.Pressure);

        _logger.LogInformation("Sensor event processed successfully for location: {Location}", notification.Location);
    }
}