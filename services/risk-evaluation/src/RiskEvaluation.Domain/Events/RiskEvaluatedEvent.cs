namespace RiskEvaluation.Domain.Events;

public class RiskEvaluatedEvent
{
    public Guid EvaluationId { get; }
    public string Location { get; }
    public double Score { get; }
    public string Level { get; }
    public DateTime Timestamp { get; }

    public RiskEvaluatedEvent(Guid evaluationId, string location, double score, string level, DateTime timestamp)
    {
        EvaluationId = evaluationId;
        Location = location;
        Score = score;
        Level = level;
        Timestamp = timestamp;
    }
}