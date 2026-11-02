using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class TemperatureAnomalyPollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TemperatureAnomalyPollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var dataSourceId = Guid.Parse(_configuration["Polling:TemperatureAnomaly:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:TemperatureAnomaly:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var response = await _httpClient.GetAsync(
            $"data?datasetid=GHCND&datatypeid=TAVG,TMAX,TMIN&startdate={startDate:yyyy-MM-dd}&enddate={endDate:yyyy-MM-dd}&stationid=GHCND:USC00250050&limit=1000",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<NoaaResponseDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data is null || data.Results.Count == 0)
            return [];

        var latitude = double.Parse(_configuration["Polling:TemperatureAnomaly:Latitude"] ?? "0");
        var longitude = double.Parse(_configuration["Polling:TemperatureAnomaly:Longitude"] ?? "0");

        var collections = data.Results
            .Where(r => r.DataType == "TAVG" || r.DataType == "TMAX" || r.DataType == "TMIN")
            .Select(SensorCollectionDTO (r) => new ClimaticCollectionDTO
            {
                DataSourceId = dataSourceId,
                TenantId = tenantId,
                CollectedAt = DateTime.UtcNow,
                Domain = SensorDomainEnum.Climatic,
                ClimaticType = ClimaticEventEnum.TemperatureAnomaly,
                Payload = JsonSerializer.Serialize(r),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        SensorValue = r.Value / 10.0, // NOAA retorna em décimos de °C
                        Unit = "Celsius",
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