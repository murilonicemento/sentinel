namespace RiskEvaluation.Application.Interfaces;

public interface IRiskEvaluationRepository
{
    Task AddAsync(RiskEvaluationEntity evaluation);
    Task<RiskEvaluationEntity?> GetByLocationAsync(string location);
    Task UpdateAsync(RiskEvaluationEntity evaluation);
}