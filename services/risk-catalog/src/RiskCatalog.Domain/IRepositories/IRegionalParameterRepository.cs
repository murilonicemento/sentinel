using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.IRepositories;

public interface IRegionalParameterRepository
{
    public Task<RegionalParameter?> GetRegionByIdAsync(
        Guid regionId,
        CancellationToken cancellationToken = default);

    public Task<RegionalParameter?> GetByAdjustmentFactorAsync(
        double adjustmentFactor,
        CancellationToken cancellationToken = default);

    public Task<bool> AddRegionalParameterAsync(
        RegionalParameter regionalParameter,
        CancellationToken cancellationToken = default);

    /// <summary>Regional parameters ordered by id.</summary>
    public Task<(IReadOnlyList<RegionalParameter> Items, long TotalItems)> GetPageAsync(int page, int pageSize,
        CancellationToken cancellationToken = default);
}
