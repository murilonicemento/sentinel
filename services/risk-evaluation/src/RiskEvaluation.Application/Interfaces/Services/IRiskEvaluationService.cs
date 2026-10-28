namespace RiskEvaluation.Application.Interfaces.Services;

public interface IRiskEvaluationService
{
    public Task<RiskEvaluationEntity> EvaluateRiskAsync(
        int latitude,
        int longitude,
        RiskMetrics metrics,
        RiskEvents events,
        EvaluationSource? source = null);
}

/// <summary>
/// What produced the evaluation: the precise position (the stored evaluation keeps whole degrees) and, for sensor
/// events, the event type and intensity used to look up the catalog risk matrix.
/// </summary>
public sealed record EvaluationSource(double Latitude, double Longitude, string? EventType = null,
    double? Intensity = null);