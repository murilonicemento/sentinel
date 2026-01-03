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
}