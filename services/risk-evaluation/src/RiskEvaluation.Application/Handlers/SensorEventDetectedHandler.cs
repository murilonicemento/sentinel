using MediatR;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Services;

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
        var latitude = (int)notification.Latitude;
        var longitude = (int)notification.Longitude;
        var metrics = MapEventTypeToRiskMetrics(notification.EventType, notification.Intensity);
        var events = MapEventTypeToRiskEvents(notification.EventType, notification.Intensity);

        await _riskEvaluationService.EvaluateRiskAsync(
            latitude,
            longitude,
            metrics,
            events);
    }

    private static RiskMetrics MapEventTypeToRiskMetrics(string eventType, double intensity)
    {
        var events = new RiskMetrics();

        switch (eventType.ToLowerInvariant())
        {
            case "temperatureanomaly":
                events.TemperatureAnomaly = intensity;
                break;
            case "humidityanomaly":
                events.HumidityAnomaly = intensity;
                break;
            case "windgust":
                events.WindGust = intensity;
                break;
            case "rainfall":
                events.Rainfall = intensity;
                break;
            case "pressurechange":
                events.PressureChange = intensity;
                break;
        }

        return events;
    }

    private static RiskEvents MapEventTypeToRiskEvents(string eventType, double intensity)
    {
        var events = new RiskEvents();
        var eventDetails = new EventDetails
        {
            Detected = true,
            Intensity = intensity
        };

        switch (eventType.ToLowerInvariant())
        {
            case "wildfire":
                events.Wildfire = eventDetails;
                break;
            case "earthquake":
                events.Earthquake = eventDetails;
                break;
            case "flood":
                events.Flood = eventDetails;
                break;
            case "landslide":
                events.Landslide = eventDetails;
                break;
        }

        return events;
    }
}