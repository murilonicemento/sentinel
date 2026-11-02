using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Domain.Repositories;

public interface ICatalogSnapshotRepository
{
    /// <summary>Inserts or replaces the snapshot of its version.</summary>
    Task SaveAsync(RiskCatalogSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<RiskCatalogSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);
}
