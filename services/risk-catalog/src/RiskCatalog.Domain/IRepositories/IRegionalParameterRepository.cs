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
}