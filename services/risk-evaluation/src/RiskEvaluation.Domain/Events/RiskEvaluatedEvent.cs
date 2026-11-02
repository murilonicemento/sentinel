namespace RiskEvaluation.Domain.Events;

public class RiskEvaluatedEvent
{
    public Guid EvaluationId { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public double Score { get; }
    public string Level { get; }
    public DateTime Timestamp { get; }

    public RiskEvaluatedEvent(
        Guid evaluationId,
        double latitude,
        double longitude,
        double score,
        string level,
        DateTime timestamp)
    {
        EvaluationId = evaluationId;
        Latitude = latitude;
        Longitude = longitude;
        Score = score;
        Level = level;
        Timestamp = timestamp;
    }
}