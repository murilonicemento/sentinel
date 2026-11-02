using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>
/// Stormglass v2 weather point, hourly, for the subscription's point. The key goes in the <c>Authorization</c>
/// header as is (set when the client is registered). Only the past window is requested: future hours are a forecast,
/// not a detection. Each hour takes Stormglass's own value (<c>sg</c>) when present, else the first source.
/// </summary>
public abstract class StormglassPollingClient(HttpClient httpClient) : ISensorPollingClient
{
    public abstract string Source { get; }

    /// <summary>Stormglass parameter (gust, precipitation, pressure).</summary>
    protected abstract string Parameter { get; }

    protected abstract string Unit { get; }
    protected abstract Func<StormglassHourDTO, Dictionary<string, double>> Values { get; }

    public async Task<List<SensorCollectionDTO>> FetchAsync(PollingSubscription subscription, PollingWindow window,
        CancellationToken cancellationToken)
    {
        var query = $"weather/point?lat={PollingRequests.Number(subscription.Latitude)}" +
                    $"&lng={PollingRequests.Number(subscription.Longitude)}&params={Parameter}" +
                    $"&start={new DateTimeOffset(window.From).ToUnixTimeSeconds()}" +
                    $"&end={new DateTimeOffset(window.To).ToUnixTimeSeconds()}";
        var json = await PollingRequests.ReadAsync(await httpClient.GetAsync(query, cancellationToken), Source,
            cancellationToken);
        var data = JsonSerializer.Deserialize<StormglassResponseDTO>(json, PollingRequests.Json);

        return (data?.Hours ?? [])
            .Select(hour => (Hour: hour, Time: DateTime.SpecifyKind(hour.Time.ToUniversalTime(), DateTimeKind.Utc),
                Values: Values(hour)))
            .Where(reading => reading.Values.Count > 0 && reading.Time <= window.To)
            .Select(SensorCollectionDTO (reading) => new ClimaticCollectionDTO
            {
                DataSourceId = subscription.DataSourceId,
                TenantId = subscription.TenantId,
                CollectedAt = reading.Time,
                SourceEventId = string.Join(':', Parameter, PollingRequests.Number(subscription.Latitude),
                    PollingRequests.Number(subscription.Longitude), reading.Time.ToString("yyyyMMddHH")),
                Domain = SensorDomainEnum.Climatic,
                Payload = JsonSerializer.Serialize(reading.Values),
                Samples =
                [
                    new SampleSensorDTO
                    {
                        SensorValue = reading.Values.TryGetValue("sg", out var sg) ? sg : reading.Values.First().Value,
                        Unit = Unit,
                        Latitude = subscription.Latitude,
                        Longitude = subscription.Longitude,
                        RecordedAt = reading.Time
                    }
                ]
            })
            .ToList();
    }
}
