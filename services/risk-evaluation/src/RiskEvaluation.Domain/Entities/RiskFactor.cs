using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Entities;

public class RiskFactor
{
    public RiskFactorType Type { get; private set; }
    public double Value { get; private set; }
    public double Weight { get; private set; }

    public RiskFactor(RiskFactorType type, double value, double weight)
    {
        Type = type;
        Value = value;
        Weight = weight;
    }
}