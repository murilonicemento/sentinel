using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class FloodPollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public FloodPollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var dataSourceId = Guid.Parse(_configuration["Polling:Flood:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:Flood:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var latitude = _configuration["Polling:Flood:Latitude"] ??
                       throw new ArgumentException("Latitude is required in config");
        var longitude = _configuration["Polling:Flood:Longitude"] ??
                        throw new ArgumentException("Longitude is required in config");
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var response = await _httpClient.GetAsync(
            $"/data?datasetid=GHCND&datatypeid=PRCP&startdate={startDate}&enddate={endDate}&stationid=GHCND:USC00250050&limit=1000",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var floodData = JsonSerializer.Deserialize<NoaaResponseDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (floodData is null || floodData.Results.Count == 0)
            return [];

        var collections = floodData.Results
            .Where(r => r.DataType == "PRCP")
            .Select(SensorCollectionDTO (r) => new DisasterCollectionDTO
            {
                DataSourceId = dataSourceId,
                TenantId = tenantId,
                CollectedAt = DateTime.UtcNow,
                Domain = SensorDomainEnum.Disaster,
                DisasterType = DisasterEventEnum.Flood,
                Payload = JsonSerializer.Serialize(r),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        SensorValue = r.Value / 10.0, // NOAA retorna em mm*10
                        Unit = "mm",
                        Latitude = double.Parse(latitude),
                        Longitude = double.Parse(longitude),
                        RecordedAt = r.Date
                    }
                ]
            })
            .ToList();

        return collections;
    }
}