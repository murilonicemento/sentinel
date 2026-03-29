using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Domain.Repositories;

/// <summary>
/// Repository for managing RiskModel entities.
/// </summary>
public interface IRiskModelRepository
{
    Task<RiskModel?> GetByVersionAsync(string version, CancellationToken cancellationToken = default);
    Task<RiskModel?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RiskModel riskModel, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiskModel>> GetAllAsync(CancellationToken cancellationToken = default);
}
