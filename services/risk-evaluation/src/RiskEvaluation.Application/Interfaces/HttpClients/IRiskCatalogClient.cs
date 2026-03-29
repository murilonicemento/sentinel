namespace RiskEvaluation.Application.Interfaces.HttpClients;

/// <summary>
/// Client for integrating with Risk Catalog service to fetch weights and rules.
/// </summary>
public interface IRiskCatalogClient
{
    public Task<RiskMatrixDTO?> GetRiskMatrixAsync(string eventTypeCode, string severityLevel, int? version = null,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<RiskParameterDTO>> GetRegionalRiskParametersAsync(Guid regionId,
        CancellationToken cancellationToken = default);

    public Task<RiskWeightsDTO?> GetLatestRiskWeightsAsync(CancellationToken cancellationToken = default);
}