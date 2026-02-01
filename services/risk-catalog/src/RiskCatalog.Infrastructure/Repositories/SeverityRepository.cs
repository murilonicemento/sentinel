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

    public async Task<bool> CreateSeverity(Severity severity, CancellationToken cancellationToken = default)
    {
        await _dbContext.Severities.AddAsync(severity, cancellationToken);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }
}