using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.Handlers;

public class SensorEventDetectedHandler : INotificationHandler<SensorEventDetected>
{
    private readonly IRiskEvaluationService _riskEvaluationService;

    public SensorEventDetectedHandler(IRiskEvaluationService riskEvaluationService)
    {
        _riskEvaluationService = riskEvaluationService;
    }

    public async Task Handle(SensorEventDetected notification, CancellationToken cancellationToken)
    {
        var metrics = new RiskMetrics
        {
            WindGust = notification.Gust,
            Rainfall = notification.Precipitation,
            PressureChange = notification.Pressure
        };

        var events = new RiskEvents();

        await _riskEvaluationService.EvaluateRiskAsync(
            notification.Latitude,
            notification.Longitude,
            metrics,
            events);
    }
}