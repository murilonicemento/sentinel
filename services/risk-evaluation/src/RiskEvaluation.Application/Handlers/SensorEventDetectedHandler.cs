using MediatR;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.Contracts;
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
        // Convert double lat/lon to int for service
        var latitude = (int)notification.Latitude;
        var longitude = (int)notification.Longitude;

        // Map event type and intensity to RiskEvents
        var events = MapEventTypeToRiskEvents(notification.EventType, notification.Intensity);

        // Empty metrics since this is a disaster/climatic event notification
        var metrics = new RiskMetrics();

        await _riskEvaluationService.EvaluateRiskAsync(
            latitude,
            longitude,
            metrics,
            events);
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