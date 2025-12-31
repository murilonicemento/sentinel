using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.IRepositories;

public interface IRegionalParameterRepository
{
    public Task<RegionalParameter?> GetByRegionIdAsync(Guid regionId);
}