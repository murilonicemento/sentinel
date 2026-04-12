namespace RiskEvaluation.Application.Interfaces.Services;

public interface IRiskEvaluationService
{
    public Task<RiskEvaluationEntity> EvaluateRiskAsync(
        int latitude,
        int longitude,
        RiskMetrics metrics,
        RiskEvents events);
}