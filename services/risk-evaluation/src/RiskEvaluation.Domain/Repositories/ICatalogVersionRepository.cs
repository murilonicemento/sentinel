using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Domain.Repositories;

public interface ICatalogVersionRepository
{
    /// <summary>Inserts or replaces the version, so a redelivered publication is harmless.</summary>
    Task SaveAsync(CatalogVersion catalogVersion, CancellationToken cancellationToken = default);

    Task<CatalogVersion?> GetLatestAsync(CancellationToken cancellationToken = default);
}
