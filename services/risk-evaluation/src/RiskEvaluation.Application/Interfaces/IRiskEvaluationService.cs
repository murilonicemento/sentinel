namespace RiskEvaluation.Application.Interfaces;

public interface IRiskEvaluationService
{
    public Task<RiskEvaluationEntity> EvaluateRiskAsync(string location, double gust, double precipitation, double pressure);
}