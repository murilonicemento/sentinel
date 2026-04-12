using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class LandslidePollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public LandslidePollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var dataSourceId = Guid.Parse(_configuration["Polling:Landslide:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:Landslide:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var latitude = _configuration["Polling:Landslide:Latitude"] ??
                       throw new ArgumentException("Latitude is required in config");
        var longitude = _configuration["Polling:Landslide:Longitude"] ??
                        throw new ArgumentException("Longitude is required in config");
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-2);
        var response = await _httpClient.GetAsync(
            $"/data?datasetid=GHCND&datatypeid=PRCP&startdate={startDate:yyyy-MM-dd}&enddate={endDate:yyyy-MM-dd}&stationid=GHCND:USC00250050&limit=1000",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var prcpData = JsonSerializer.Deserialize<NoaaResponseDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (prcpData is null || prcpData.Results.Count == 0)
            return [];

        var totalPrecipitation = prcpData.Results
            .Where(r => r.DataType == "PRCP")
            .Sum(r => r.Value / 10.0); // NOAA retorna décimos de mm
        var collections = new List<SensorCollectionDTO>
        {
            new DisasterCollectionDTO
            {
                DataSourceId = dataSourceId,
                TenantId = tenantId,
                CollectedAt = DateTime.UtcNow,
                Domain = SensorDomainEnum.Disaster,
                DisasterType = DisasterEventEnum.Landslide,
                Payload = JsonSerializer.Serialize(prcpData.Results),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        SensorValue = totalPrecipitation,
                        Unit = "mm",
                        Latitude = double.Parse(latitude),
                        Longitude = double.Parse(longitude),
                        RecordedAt = DateTime.UtcNow
                    }
                ]
            }
        };

        return collections;
    }
}