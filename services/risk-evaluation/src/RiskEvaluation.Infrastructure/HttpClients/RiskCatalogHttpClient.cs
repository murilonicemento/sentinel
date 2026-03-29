using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Interfaces.HttpClients;

namespace RiskEvaluation.Infrastructure.HttpClients;

public class RiskCatalogHttpClient : IRiskCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RiskCatalogHttpClient> _logger;

    public RiskCatalogHttpClient(HttpClient httpClient, ILogger<RiskCatalogHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RiskMatrixDTO?> GetRiskMatrixAsync(string eventTypeCode, string severityLevel, int? version = null, CancellationToken cancellationToken = default)
    {
        var url = $"/api/risk-model/risk-matrix?eventTypeCode={eventTypeCode}&severityLevel={severityLevel}";
        
        if (version.HasValue)
            url += $"&version={version.Value}";
            
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
            
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RiskMatrixDTO>(cancellationToken);
    }

    public async Task<IReadOnlyList<RiskParameterDTO>> GetRegionalRiskParametersAsync(Guid regionId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/risk-model/regional-risk-parameters?regionId={regionId}", cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            return new List<RiskParameterDTO>();
            
        var data = await response.Content.ReadFromJsonAsync<List<RiskParameterDTO>>(cancellationToken);
        return data ?? [];
    }

    public async Task<RiskWeightsDTO?> GetLatestRiskWeightsAsync(CancellationToken cancellationToken = default)
    {
        // Get the latest published catalog version
        var response = await _httpClient.GetAsync("/api/risk-model/catalog/latest", cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
            
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RiskWeightsDTO>(cancellationToken);
    }
}
