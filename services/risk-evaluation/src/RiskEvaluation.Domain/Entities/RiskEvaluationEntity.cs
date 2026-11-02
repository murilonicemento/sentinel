using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Entities;

public class RiskEvaluationEntity
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Position evaluated. Evaluations stored before RE-18 hold it truncated to whole degrees; Mongo reads those
    /// integers as doubles.
    /// </summary>
    public double Latitude { get; private set; }

    public double Longitude { get; private set; }
    public DateTime Timestamp { get; private set; }
    public double Score { get; private set; }
    public RiskLevel Level { get; private set; }

    public RiskEvaluationEntity(double latitude, double longitude, double score, RiskLevel level)
    {
        Id = Guid.NewGuid();
        Latitude = latitude;
        Longitude = longitude;
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