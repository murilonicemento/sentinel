using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.Infrastructure.Persistence.DatabaseContext;

namespace RiskCatalog.Infrastructure.Persistence.Repositories;

public class RiskMatrixRepository : IRiskMatrixRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public RiskMatrixRepository(RiskCatalogDbContext riskCatalogDbContext)
    {
        _dbContext = riskCatalogDbContext;
    }

    public async Task<RiskMatrix?> GetRiskMatrixForEventTypeAsync(
        string eventTypeCode,
        string severityLevel,
        int? version, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RiskMatrices
            .Include(riskMatrix => riskMatrix.EventType)
            .FirstOrDefaultAsync(riskMatrix =>
                riskMatrix.EventType.Code == eventTypeCode &&
                riskMatrix.SeverityLevel.ToString() == severityLevel &&
                (version == null || riskMatrix.Version == version), cancellationToken);
    }

    public async Task<bool> AddRiskMatrixAsync(RiskMatrix riskMatrix, CancellationToken cancellationToken = default)
    {
        await _dbContext.RiskMatrices.AddAsync(riskMatrix, cancellationToken);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }

    public async Task<bool> MarkVersionAsActiveAsync(int version, CancellationToken cancellationToken = default)
    {
        var matricesWithVersion = await _dbContext.RiskMatrices
            .Where(rm => rm.Version == version)
            .ToListAsync(cancellationToken);

        return matricesWithVersion.Count != 0;
    }

    public async Task<(IReadOnlyList<RiskMatrix> Items, long TotalItems)> GetPageAsync(int? version, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RiskMatrices.AsNoTracking().Include(m => m.EventType).AsQueryable();
        if (version.HasValue) query = query.Where(m => m.Version == version.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderBy(m => m.EventType.Code).ThenBy(m => m.SeverityLevel).ThenBy(m => m.Version).ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
