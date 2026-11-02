using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>
/// NASA FIRMS area API (VIIRS S-NPP near real time): fire detections in the subscription's bounding box. The map key
/// is part of the path, so it is added per request and the client is never logged with it.
/// </summary>
public class FirePollingHttpClient(HttpClient httpClient, string mapKey) : ISensorPollingClient
{
    public string Source => PollingSources.Fire;

    public async Task<List<SensorCollectionDTO>> FetchAsync(PollingSubscription subscription, PollingWindow window,
        CancellationToken cancellationToken)
    {
        var (west, south, east, north) = PollingRequests.BoundingBox(subscription);
        var days = Math.Clamp((int)Math.Ceiling((window.To - window.From).TotalDays), 1, 10);
        var area = string.Join(',', new[] { west, south, east, north }.Select(PollingRequests.Number));
        var csvContent = await PollingRequests.ReadAsync(
            await httpClient.GetAsync($"api/area/csv/{mapKey}/VIIRS_SNPP_NRT/{area}/{days}", cancellationToken),
            Source, cancellationToken);

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<FireFirmsApiResponseDTO>()
            .Select(fire => (Fire: fire, DetectedAt: ParseFirmsDateTime(fire.AcqDate, fire.AcqTime)))
            .Where(detection => detection.DetectedAt >= window.From && detection.DetectedAt <= window.To)
            .Select(SensorCollectionDTO (detection) => new DisasterCollectionDTO
            {
                DataSourceId = subscription.DataSourceId,
                TenantId = subscription.TenantId,
                CollectedAt = detection.DetectedAt,
                SourceEventId = string.Join(':', detection.Fire.Satellite,
                    PollingRequests.Number(detection.Fire.Latitude), PollingRequests.Number(detection.Fire.Longitude),
                    detection.Fire.AcqDate, detection.Fire.AcqTime),
                Domain = SensorDomainEnum.Disaster,
                DisasterType = DisasterEventEnum.Wildfire,
                Payload = JsonSerializer.Serialize(detection.Fire),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        // Fire radiative power, the FIRMS measure of fire intensity.
                        SensorValue = detection.Fire.Frp,
                        Unit = "MW",
                        Latitude = detection.Fire.Latitude,
                        Longitude = detection.Fire.Longitude,
                        RecordedAt = detection.DetectedAt
                    }
                ]
            })
            .ToList();
    }

    private static DateTime ParseFirmsDateTime(string date, string time)
    {
        var paddedTime = time.PadLeft(4, '0');
        var baseDate = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, int.Parse(paddedTime[..2]),
            int.Parse(paddedTime[2..4]), 0, DateTimeKind.Utc);
    }
}
