using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.IRepositories;

public interface IRegionalParameterRepository
{
    public Task<RegionalParameter?> GetByRegionIdAsync(Guid regionId, CancellationToken cancellationToken = default);

    public Task<bool> AddRegionalParameterAsync(
        RegionalParameter regionalParameter,
        CancellationToken cancellationToken = default);
}