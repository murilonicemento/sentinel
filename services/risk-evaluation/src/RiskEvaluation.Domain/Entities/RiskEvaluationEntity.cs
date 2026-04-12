using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Entities;

public class RiskEvaluationEntity
{
    public Guid Id { get; private set; }
    public int Latitude { get; private set; }
    public int Longitude { get; private set; }
    public DateTime Timestamp { get; private set; }
    public double Score { get; private set; }
    public RiskLevel Level { get; private set; }
    
    public RiskEvaluationEntity(int latitude, int longitude, double score, RiskLevel level)
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