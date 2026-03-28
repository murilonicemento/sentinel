using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.IntegrationClients;

namespace RiskEvaluation.Infrastructure.IntegrationClients;

public class IngestionHttpClient : IIngestionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IngestionHttpClient> _logger;

    public IngestionHttpClient(HttpClient httpClient, ILogger<IngestionHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SensorDataDto?> GetLatestSensorDataAsync(int latitude, int longitude, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/sensors/data/latest?lat={latitude}&lon={longitude}", cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
            
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SensorDataDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<SensorDataDto>> GetSensorDataHistoryAsync(int latitude, int longitude, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/sensors/data/history?lat={latitude}&lon={longitude}&from={from:O}&to={to:O}", cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            return new List<SensorDataDto>();
            
        var data = await response.Content.ReadFromJsonAsync<List<SensorDataDto>>(cancellationToken);
        return data ?? new List<SensorDataDto>();
    }
}
