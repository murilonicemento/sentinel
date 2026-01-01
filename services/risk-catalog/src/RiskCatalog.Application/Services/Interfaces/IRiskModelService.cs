using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Services.Interfaces;

public interface IRiskModelService
{
    public Task<RiskMatrixDTO?> GetRiskMatrixForEventTypeAsync(
        string eventTypeCode,
        string severityLevel,
        int? version,
        CancellationToken cancellationToken = default);

    public Task<IDFCurvesDTO?> GetIDFCurvesForEventTypeAsync(
        string eventTypeCode,
        int? returnPeriodYears,
        CancellationToken cancellationToken = default);

    public Task<RegionalRiskParametersDTO?> GetRegionalRiskParametersAsync(
        Guid regionId,
        CancellationToken cancellationToken = default);

    public Task<RiskMatrixDTO> CreateRiskMatrixAsync(
        RiskMatrixDTO riskMatrixDto,
        CancellationToken cancellationToken = default);
    
    public  Task<RiskMatrixDTO> CreateIDFCurvesAsync(
        CreateIDFCurvesDTO idfCurvesDto,
        CancellationToken cancellationToken = default);
}