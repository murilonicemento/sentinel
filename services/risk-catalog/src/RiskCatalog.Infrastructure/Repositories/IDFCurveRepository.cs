using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.Infrastructure.DatabaseContext;

namespace RiskCatalog.Infrastructure.Repositories;

public class IDFCurveRepository : IIDFCurveRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public IDFCurveRepository(RiskCatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IDFCurve?> GetIDFCurveForEventTypeAsync(string eventTypeCode, int? returnPeriodYears)
    {
        return await _dbContext.IDFCurves.FirstOrDefaultAsync(idfCurve =>
            idfCurve.EventType.Code == eventTypeCode &&
            (returnPeriodYears == null || idfCurve.ReturnPeriodYears == returnPeriodYears));
    }
}