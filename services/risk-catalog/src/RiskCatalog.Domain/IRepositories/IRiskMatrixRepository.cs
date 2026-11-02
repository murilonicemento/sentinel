using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.IRepositories;

public interface IRiskMatrixRepository
{
    public Task<RiskMatrix?> GetRiskMatrixForEventTypeAsync(
        string eventTypeCode,
        string severityLevel,
        int? version,
        CancellationToken cancellationToken = default);

    public Task<bool> AddRiskMatrixAsync(RiskMatrix riskMatrix, CancellationToken cancellationToken = default);

    public Task<bool> MarkVersionAsActiveAsync(int version, CancellationToken cancellationToken = default);

    /// <summary>Matrix entries with their event type, ordered by event type code, severity, version and id.</summary>
    public Task<(IReadOnlyList<RiskMatrix> Items, long TotalItems)> GetPageAsync(int? version, int page, int pageSize,
        CancellationToken cancellationToken = default);
}
