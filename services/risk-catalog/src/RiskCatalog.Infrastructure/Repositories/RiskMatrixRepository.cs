using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.Infrastructure.DatabaseContext;

namespace RiskCatalog.Infrastructure.Repositories;

public class RiskMatrixRepository : IRiskMatrixRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public RiskMatrixRepository(RiskCatalogDbContext riskCatalogDbContext)
    {
        _dbContext = riskCatalogDbContext;
    }

    public async Task<RiskMatrix?> GetRiskMatrixForEventTypeAsync(string eventTypeCode, string severityLevel,
        int? version)
    {
        return await _dbContext.RiskMatrices.FirstOrDefaultAsync(riskMatrix =>
            riskMatrix.EventType.Code == eventTypeCode &&
            riskMatrix.SeverityLevel.ToString() == severityLevel &&
            (version == null || riskMatrix.Version == version));
    }
}