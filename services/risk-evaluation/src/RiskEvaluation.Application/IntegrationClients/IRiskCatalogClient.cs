namespace RiskEvaluation.Application.IntegrationClients;

/// <summary>
/// Client for integrating with Risk Catalog service to fetch weights and rules.
/// </summary>
public interface IRiskCatalogClient
{
    public Task<RiskMatrixDto?> GetRiskMatrixAsync(string eventTypeCode, string severityLevel, int? version = null, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<RiskParameterDto>> GetRegionalRiskParametersAsync(Guid regionId, CancellationToken cancellationToken = default);
    public Task<RiskWeightsDto?> GetLatestRiskWeightsAsync(CancellationToken cancellationToken = default);
}

public class RiskMatrixDto
{
    public string EventTypeCode { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public int Version { get; set; }
    public Dictionary<string, double> Weights { get; set; } = new();
    public List<RiskRuleDto> Rules { get; set; } = new();
}

public class RiskRuleDto
{
    public string Condition { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string Action { get; set; } = string.Empty;
}

public class RiskParameterDto
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Region { get; set; } = string.Empty;
}

public class RiskWeightsDto
{
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, double> MetricWeights { get; set; } = new();
    public Dictionary<string, double> EventWeights { get; set; } = new();
    public DateTime EffectiveDate { get; set; }
}
