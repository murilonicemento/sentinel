using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>USGS FDSN event service (no key): earthquakes within the subscription's radius.</summary>
public class EarthquakePollingHttpClient(HttpClient httpClient) : ISensorPollingClient
{
    public string Source => PollingSources.Earthquake;

    public async Task<List<SensorCollectionDTO>> FetchAsync(PollingSubscription subscription, PollingWindow window,
        CancellationToken cancellationToken)
    {
        var query = "fdsnws/event/1/query?format=geojson" +
                    $"&starttime={window.From:yyyy-MM-ddTHH:mm:ss}&endtime={window.To:yyyy-MM-ddTHH:mm:ss}" +
                    $"&latitude={PollingRequests.Number(subscription.Latitude)}" +
                    $"&longitude={PollingRequests.Number(subscription.Longitude)}" +
                    $"&maxradiuskm={PollingRequests.Number(Math.Min(subscription.RadiusKm, 20_000))}";
        var json = await PollingRequests.ReadAsync(await httpClient.GetAsync(query, cancellationToken), Source,
            cancellationToken);
        var featureCollection = JsonSerializer.Deserialize<EarthquakeFeatureCollection>(json, PollingRequests.Json);

        // USGS may omit properties, geometry, magnitude or time; without them there is nothing to evaluate.
        return (featureCollection?.Features ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Id) && f.Properties is { Mag: not null, Time: not null } &&
                        f.Geometry?.Coordinates is { Length: >= 2 })
            .Select(SensorCollectionDTO (f) =>
            {
                var occurredAt = DateTimeOffset.FromUnixTimeMilliseconds(f.Properties!.Time!.Value).UtcDateTime;
                return new DisasterCollectionDTO
                {
                    DataSourceId = subscription.DataSourceId,
                    TenantId = subscription.TenantId,
                    CollectedAt = occurredAt,
                    SourceEventId = f.Id,
                    Domain = SensorDomainEnum.Disaster,
                    DisasterType = DisasterEventEnum.Earthquake,
                    Payload = JsonSerializer.Serialize(f),
                    Samples =
                    [
                        new SampleSensorDTO
                        {
                            // The magnitude type (mww, mb, ml...) stays in the payload; the domain unit is Mw.
                            SensorValue = f.Properties.Mag!.Value,
                            Unit = "Mw",
                            Latitude = f.Geometry!.Coordinates[1],
                            Longitude = f.Geometry.Coordinates[0],
                            RecordedAt = occurredAt
                        }
                    ]
                };
            })
            .ToList();
    }
}
