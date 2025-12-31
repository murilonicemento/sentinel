using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.Infrastructure.DatabaseContext;

namespace RiskCatalog.Infrastructure.Repositories;

public class RegionalParameterRepository : IRegionalParameterRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public RegionalParameterRepository(RiskCatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegionalParameter?> GetByRegionIdAsync(Guid regionId)
    {
        return await _dbContext.RegionalParameters.FirstOrDefaultAsync(regionalParameter =>
            regionalParameter.RegionId == regionId);
    }
}