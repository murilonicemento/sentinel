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

    public async Task<IDFCurve?> GetIDFCurveForEventTypeAsync(
        string eventTypeCode,
        int? returnPeriodYears,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.IDFCurves
            .Include(idfCurve => idfCurve.EventType)
            .FirstOrDefaultAsync(idfCurve =>
                idfCurve.EventType.Code == eventTypeCode &&
                (!returnPeriodYears.HasValue || idfCurve.ReturnPeriodYears == returnPeriodYears), cancellationToken);
    }

    public async Task<IDFCurve?> GetIDFCurveForDurationAndPeriod(
        string eventTypeCode,
        int durationMinutes,
        int returnPeriodYears, CancellationToken cancellationToken = default)
    {
        return await _dbContext.IDFCurves.FirstOrDefaultAsync(idfCurve =>
            idfCurve.EventType.Code == eventTypeCode &&
            idfCurve.DurationMinutes == durationMinutes &&
            idfCurve.ReturnPeriodYears == returnPeriodYears, cancellationToken);
    }

    public async Task<bool> AddIDFCurveAsync(IDFCurve idfCurve, CancellationToken cancellationToken = default)
    {
        await _dbContext.IDFCurves.AddAsync(idfCurve, cancellationToken);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }

    public async Task<bool> MarkVersionAsActiveAsync(int version, CancellationToken cancellationToken = default)
    {
        var curvesWithVersion = await _dbContext.IDFCurves
            .Where(idf => idf.Version == version)
            .ToListAsync(cancellationToken);

        return curvesWithVersion.Count != 0;
    }
}