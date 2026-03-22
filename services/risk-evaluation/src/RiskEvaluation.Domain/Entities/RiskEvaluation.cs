using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Entities;

public class RiskEvaluationEntity
{
    public Guid Id { get; private set; }
    public string Location { get; private set; }
    public DateTime Timestamp { get; private set; }
    public double Score { get; private set; }
    public RiskLevel Level { get; private set; }

    private RiskEvaluationEntity() { } // For EF or serialization

    public RiskEvaluationEntity(string location, double score, RiskLevel level)
    {
        Id = Guid.NewGuid();
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Timestamp = DateTime.UtcNow;
        Score = score;
        Level = level;
    }

    public void UpdateScore(double newScore, RiskLevel newLevel)
    {
        Score = newScore;
        Level = newLevel;
        Timestamp = DateTime.UtcNow;
    }
}