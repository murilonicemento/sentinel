using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class FirePollingHttpClient : ISensorPollingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public FirePollingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken)
    {
        var mapKey = _configuration["Polling:Fire:MapKey"] ??
                     throw new ArgumentException("MapKey is required in config");
        var dataSourceId = Guid.Parse(_configuration["Polling:Fire:DataSourceId"] ??
                                      throw new ArgumentException("DataSourceId is required in config"));
        var tenantId = Guid.Parse(_configuration["Polling:Fire:TenantId"] ??
                                  throw new ArgumentException("TenantId is required in config"));
        var area = _configuration["Polling:Fire:Area"]! ?? throw new ArgumentException("Area is required in config");
        var days = _configuration["Polling:Fire:Days"]! ?? throw new ArgumentException("Days is required in config");
        var date = _configuration["Polling:Fire:Date"]! ?? throw new ArgumentException("Date is required in config");
        var response =
            await _httpClient.GetAsync($"api/area/csv/{mapKey}/VIIRS_SNPP_NRT/{area}/{days}/{date}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var csvContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var fires = csv.GetRecords<FireFirmsApiResponseDTO>().ToList();
        var collections = fires.Select(SensorCollectionDTO (fire) =>
        {
            var recordedAt = ParseFirmsDateTime(fire.AcqDate, fire.AcqTime);

            return new DisasterCollectionDTO
            {
                DataSourceId = dataSourceId,
                TenantId = tenantId,
                CollectedAt = DateTime.UtcNow,
                Domain = SensorDomainEnum.Disaster,
                DisasterType = DisasterEventEnum.Wildfire,
                Payload = JsonSerializer.Serialize(fire),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        SensorValue = fire.BrightTi4,
                        Unit = "Kelvin",
                        Latitude = fire.Latitude,
                        Longitude = fire.Longitude,
                        RecordedAt = recordedAt
                    }
                ]
            };
        }).ToList();

        return collections;
    }

    private static DateTime ParseFirmsDateTime(string date, string time)
    {
        var paddedTime = time.PadLeft(4, '0');
        var hour = int.Parse(paddedTime.Substring(0, 2));
        var minute = int.Parse(paddedTime.Substring(2, 2));
        var baseDate = DateTime.Parse(date, CultureInfo.InvariantCulture);

        return new DateTime(
            baseDate.Year,
            baseDate.Month,
            baseDate.Day,
            hour,
            minute,
            0,
            DateTimeKind.Utc
        );
    }
}