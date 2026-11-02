using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Repositories;

/// <summary>
/// Repository for managing RiskFactor weight configurations.
/// </summary>
public interface IRiskFactorRepository
{
    Task<RiskFactor?> GetByTypeAsync(RiskFactorType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiskFactor>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiskFactor>> GetByTypesAsync(IEnumerable<RiskFactorType> types, CancellationToken cancellationToken = default);
    Task SaveAsync(RiskFactor riskFactor, CancellationToken cancellationToken = default);
    Task SaveManyAsync(IEnumerable<RiskFactor> riskFactors, CancellationToken cancellationToken = default);
    Task DeleteByTypeAsync(RiskFactorType type, CancellationToken cancellationToken = default);
}
