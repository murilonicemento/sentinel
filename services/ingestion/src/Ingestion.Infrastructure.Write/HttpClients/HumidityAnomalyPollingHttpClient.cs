using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class HumidityAnomalyPollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public HumidityAnomalyPollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var dataSourceId = Guid.Parse(_configuration["Polling:HumidityAnomaly:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:HumidityAnomaly:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var response = await _httpClient.GetAsync(
            $"data?datasetid=GHCND&datatypeid=HUMD&startdate={startDate:yyyy-MM-dd}&enddate={endDate:yyyy-MM-dd}&stationid=GHCND:USC00250050&limit=1000",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<NoaaResponseDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data is null || data.Results.Count == 0)
            return [];

        var latitude = double.Parse(_configuration["Polling:HumidityAnomaly:Latitude"] ?? "0");
        var longitude = double.Parse(_configuration["Polling:HumidityAnomaly:Longitude"] ?? "0");

        var collections = data.Results
            .Where(r => r.DataType == "HUMD")
            .Select(SensorCollectionDTO (r) => new ClimaticCollectionDTO
            {
                DataSourceId = dataSourceId,
                TenantId = tenantId,
                CollectedAt = DateTime.UtcNow,
                Domain = SensorDomainEnum.Climatic,
                ClimaticType = ClimaticEventEnum.HumidityAnomaly,
                Payload = JsonSerializer.Serialize(r),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        SensorValue = r.Value,
                        Unit = "Percent",
                        Latitude = latitude,
                        Longitude = longitude,
                        RecordedAt = r.Date
                    }
                ]
            })
            .ToList();

        return collections;
    }
}