using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class RainfallPollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public RainfallPollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var dataSourceId = Guid.Parse(_configuration["Polling:Rainfall:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:Rainfall:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var latitude = _configuration["Polling:Rainfall:Latitude"] ??
                       throw new ArgumentException("Latitude is required in config");
        var longitude = _configuration["Polling:Rainfall:Longitude"] ??
                        throw new ArgumentException("Longitude is required in config");
        var response = await _httpClient.GetAsync($"/weather/point?lat={latitude}&lng={longitude}&params=precipitation",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<StormglassResponseDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data is null || data.Hours.Count == 0)
            return [];

        var collections = data.Hours.Select(SensorCollectionDTO (hour) => new ClimaticCollectionDTO
        {
            DataSourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            Domain = SensorDomainEnum.Climatic,
            ClimaticType = ClimaticEventEnum.Rainfall,
            Payload = JsonSerializer.Serialize(hour.Precipitation),
            Samples = hour.Precipitation.Select(p => new SampleSensorDTO
            {
                SensorValue = p.Value,
                Unit = "mm",
                Latitude = data.Meta.Lat,
                Longitude = data.Meta.Lng,
                RecordedAt = hour.Time
            }).ToList()
        }).ToList();

        return collections;
    }
}