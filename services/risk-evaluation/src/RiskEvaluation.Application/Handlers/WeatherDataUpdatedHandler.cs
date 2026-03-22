using MediatR;
using RiskEvaluation.Application.Interfaces;

namespace RiskEvaluation.Application.Handlers;

public class WeatherDataUpdatedHandler : INotificationHandler<WeatherDataUpdated>
{
    private readonly IRiskEvaluationService _riskEvaluationService;

    public WeatherDataUpdatedHandler(IRiskEvaluationService riskEvaluationService)
    {
        _riskEvaluationService = riskEvaluationService;
    }

    public async Task Handle(WeatherDataUpdated notification, CancellationToken cancellationToken)
    {
        await _riskEvaluationService.EvaluateRiskAsync(
            notification.Location,
            notification.Gust,
            notification.Precipitation,
            notification.Pressure);
    }
}