using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.IRepositories;

public interface IIDFCurveRepository
{
    public Task<IDFCurve?> GetIDFCurveForEventTypeAsync(string eventTypeCode, int? returnPeriodYears);
}