using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.Interfaces;

public interface IRiskEvaluationService
{
    public Task<RiskEvaluationEntity> EvaluateRiskAsync(
        int latitude,
        int longitude,
        RiskMetrics metrics,
        RiskEvents events);
}