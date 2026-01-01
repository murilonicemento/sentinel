using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Services;

public class RiskModelService : IRiskModelService
{
    private readonly IRiskMatrixRepository _riskMatrixRepository;
    private readonly IIDFCurveRepository _IDFCurveRepository;
    private readonly IRegionalParameterRepository _regionalParameterRepository;

    public RiskModelService(
        IRiskMatrixRepository riskMatrixRepository,
        IIDFCurveRepository idfCurveRepository,
        IRegionalParameterRepository regionalParameterRepository)
    {
        _riskMatrixRepository = riskMatrixRepository;
        _IDFCurveRepository = idfCurveRepository;
        _regionalParameterRepository = regionalParameterRepository;
    }

    public async Task<RiskMatrixDTO?> GetRiskMatrixForEventTypeAsync(
        string eventTypeCode,
        string severityLevel,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var riskMatrix = await _riskMatrixRepository.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version);

        if (riskMatrix is null)
            return null;

        return new RiskMatrixDTO
        {
            EventTypeCode = riskMatrix.EventType.Code,
            SeverityLevel = riskMatrix.SeverityLevel.ToString(),
            RiskLevel = riskMatrix.RiskLevel.ToString(),
            Version = riskMatrix.Version
        };
    }

    public async Task<IDFCurvesDTO?> GetIDFCurvesForEventTypeAsync(
        string eventTypeCode,
        int? returnPeriodYears,
        CancellationToken cancellationToken = default)
    {
        var IDFCurve = await _IDFCurveRepository.GetIDFCurveForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears);

        if (IDFCurve == null)
            return null;

        return new IDFCurvesDTO
        {
            DurationMinutes = IDFCurve.DurationMinutes,
            Intensity = IDFCurve.Intensity,
            ReturnPeriodYears = IDFCurve.ReturnPeriodYears,
            Version = IDFCurve.Version,
        };
    }

    public async Task<RegionalRiskParametersDTO?> GetRegionalRiskParametersAsync(
        Guid regionId,
        CancellationToken cancellationToken = default)
    {
        var regionalParameter = await _regionalParameterRepository.GetByRegionIdAsync(regionId);

        if (regionalParameter is null)
            return null;

        return new RegionalRiskParametersDTO
        {
            RegionId = regionalParameter.RegionId,
            AdjustmentFactor = regionalParameter.AdjustmentFactor,
            Description = regionalParameter.Description
        };
    }
}

