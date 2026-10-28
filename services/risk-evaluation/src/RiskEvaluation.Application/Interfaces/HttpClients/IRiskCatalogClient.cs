using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Application.Interfaces.HttpClients;

public interface IRiskCatalogClient
{
    /// <summary>
    /// Reads regional parameters, severity criteria and matrix entries of <paramref name="version"/> from risk-catalog.
    /// Throws when risk-catalog cannot answer, so the caller retries instead of saving a partial copy.
    /// </summary>
    Task<RiskCatalogSnapshot> FetchSnapshotAsync(int version, CancellationToken cancellationToken = default);
}
