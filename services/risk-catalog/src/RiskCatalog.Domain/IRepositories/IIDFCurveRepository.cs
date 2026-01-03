using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.IRepositories;

public interface IIDFCurveRepository
{
    public Task<IDFCurve?> GetIDFCurveForEventTypeAsync(
        string eventTypeCode,
        int? returnPeriodYears,
        CancellationToken cancellationToken = default);

    public Task<IDFCurve?> GetIDFCurveForDurationAndPeriod(
        string eventTypeCode,
        int durationMinutes,
        int returnPeriodYears,
        CancellationToken cancellationToken = default);

    public Task<bool> AddIDFCurveAsync(
        IDFCurve idfCurve,
        CancellationToken cancellationToken = default);
}