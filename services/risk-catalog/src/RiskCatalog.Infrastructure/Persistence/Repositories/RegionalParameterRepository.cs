using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.Infrastructure.Persistence.DatabaseContext;

namespace RiskCatalog.Infrastructure.Persistence.Repositories;

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

    public async Task<(IReadOnlyList<RegionalParameter> Items, long TotalItems)> GetPageAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RegionalParameters.AsNoTracking();
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
