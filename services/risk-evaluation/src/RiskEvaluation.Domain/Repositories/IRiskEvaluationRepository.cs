using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Domain.Repositories;

public interface IRiskEvaluationRepository
{
    public Task AddAsync(RiskEvaluationEntity evaluation);
    public Task<RiskEvaluationEntity?> GetByIdAsync(Guid id);
    /// <summary>
    /// Evaluations in the whole-degree cell of (<paramref name="latitude"/>, <paramref name="longitude"/>): the positions
    /// that truncate to these degrees, as the history always grouped them (RE-18).
    /// </summary>
    public Task<List<RiskEvaluationEntity>> GetByLocationAsync(int latitude, int longitude);
    public Task UpdateAsync(RiskEvaluationEntity evaluation);
}