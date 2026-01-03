using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Infrastructure.DatabaseContext;

namespace RiskCatalog.Infrastructure.Repositories;

public class SeverityRepository : ISeverityRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public SeverityRepository(RiskCatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Severity?> GetByLevel(
        SeverityLevelEnum severityLevel,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Severities.FirstOrDefaultAsync(x => x.Level == severityLevel, cancellationToken);
    }
}