using System.Globalization;
using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class EarthquakePollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public EarthquakePollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var dataSourceId = Guid.Parse(_configuration["Polling:Earthquake:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:Earthquake:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var startTime = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var endTime = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var response =
            await _httpClient.GetAsync($"fdsnws/event/1/query?format=geojson&starttime={startTime}&endtime={endTime}",
                cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var featureCollection = JsonSerializer.Deserialize<EarthquakeFeatureCollection>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (featureCollection?.Features is null)
            return [];

        var collections = featureCollection.Features.Select(SensorCollectionDTO (f) => new DisasterCollectionDTO
        {
            DataSourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = f.Properties.Time is not null
                ? DateTimeOffset.FromUnixTimeMilliseconds(f.Properties.Time.Value).UtcDateTime
                : null,
            Domain = SensorDomainEnum.Disaster,
            DisasterType = DisasterEventEnum.Earthquake,
            Payload = JsonSerializer.Serialize(f),
            Samples =
            [
                new SampleSensorDTO
                {
                    SensorValue = f.Properties.Mag ?? 0,
                    Unit = f.Properties.MagType,
                    Latitude = f.Geometry.Coordinates[1],
                    Longitude = f.Geometry.Coordinates[0],
                    RecordedAt = DateTimeOffset.FromUnixTimeMilliseconds(f.Properties.Time.GetValueOrDefault(
                        new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds())).UtcDateTime
                }
            ]
        }).ToList();

        return collections;
    }
}