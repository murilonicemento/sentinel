using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Domain.Repositories;

public interface IRiskEvaluationRepository
{
    public Task AddAsync(RiskEvaluationEntity evaluation);
    public Task<RiskEvaluationEntity?> GetByIdAsync(Guid id);
    public Task<List<RiskEvaluationEntity>> GetByLocationAsync(int latitude, int longitude);
    public Task UpdateAsync(RiskEvaluationEntity evaluation);
}