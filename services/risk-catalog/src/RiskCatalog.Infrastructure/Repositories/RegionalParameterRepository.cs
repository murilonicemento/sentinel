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

    public async Task<RegionalParameter?> GetRegionByIdAsync(Guid regionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegionalParameters
            .FirstOrDefaultAsync(regionalParameter => regionalParameter.Id == regionId,
                cancellationToken: cancellationToken);
    }

    public async Task<RegionalParameter?> GetByAdjustmentFactorAsync(
        double adjustmentFactor,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegionalParameters.FirstOrDefaultAsync(regionalParameter =>
            regionalParameter.AdjustmentFactor == adjustmentFactor, cancellationToken);
    }

    public async Task<bool> AddRegionalParameterAsync(
        RegionalParameter regionalParameter,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RegionalParameters.AddAsync(regionalParameter, cancellationToken);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }
}