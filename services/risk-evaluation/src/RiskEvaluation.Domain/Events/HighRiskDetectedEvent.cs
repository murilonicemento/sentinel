namespace RiskEvaluation.Domain.Events;

public class HighRiskDetectedEvent
{
    public Guid EvaluationId { get; }
    public int Latitude { get; }
    public int Longitude { get; }
    public double Score { get; }
    public string Level { get; }
    public DateTime Timestamp { get; }

    public HighRiskDetectedEvent(
        Guid evaluationId,
        int latitude,
        int longitude,
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