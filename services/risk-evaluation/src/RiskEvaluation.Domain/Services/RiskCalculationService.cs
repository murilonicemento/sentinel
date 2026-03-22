using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Services;

public class RiskCalculationService
{
    public double CalculateRiskScore(double gust, double precipitation, double pressure)
    {
        // Weighted sum: (gust * 0.4) + (precipitation * 0.4) + (pressure * 0.2)
        double rawScore = (gust * 0.4) + (precipitation * 0.4) + (pressure * 0.2);

        // Normalize to 0-1, assuming max possible is some value, but for simplicity, assume rawScore is already 0-1 or normalize based on typical ranges.
        // Since weights sum to 1, and if inputs are normalized, rawScore is 0-1.
        // But to be safe, clamp to 0-1.
        return Math.Clamp(rawScore, 0.0, 1.0);
    }

    public RiskLevel ClassifyRiskLevel(double score)
    {
        if (score < 0.3) return RiskLevel.Low;
        if (score < 0.6) return RiskLevel.Medium;
        if (score < 0.8) return RiskLevel.High;
        return RiskLevel.Critical;
    }
}