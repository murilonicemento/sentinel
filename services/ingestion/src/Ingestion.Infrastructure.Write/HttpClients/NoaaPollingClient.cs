using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Domain.Enums;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>
/// NOAA NCEI CDO v2, dataset GHCND (daily summaries), in metric units, for the subscription's station. The token goes
/// in the <c>token</c> header (set when the client is registered).
/// </summary>
public abstract class NoaaPollingClient(HttpClient httpClient) : ISensorPollingClient
{
    public abstract string Source { get; }

    /// <summary>GHCND data types requested (e.g. PRCP).</summary>
    protected abstract string[] DataTypes { get; }

    public async Task<List<SensorCollectionDTO>> FetchAsync(PollingSubscription subscription, PollingWindow window,
        CancellationToken cancellationToken)
    {
        var query = $"data?datasetid=GHCND&datatypeid={string.Join(',', DataTypes)}" +
                    $"&stationid={Uri.EscapeDataString(subscription.StationId!)}" +
                    $"&startdate={window.From:yyyy-MM-dd}&enddate={window.To:yyyy-MM-dd}&units=metric&limit=1000";
        var json = await PollingRequests.ReadAsync(await httpClient.GetAsync(query, cancellationToken), Source,
            cancellationToken);
        var data = JsonSerializer.Deserialize<NoaaResponseDTO>(json, PollingRequests.Json);
        var results = (data?.Results ?? []).Where(r => DataTypes.Contains(r.DataType)).ToList();
        return results.Count == 0 ? [] : Map(subscription, window, results);
    }

    /// <summary>One collection per daily value; sources that aggregate the window override it.</summary>
    protected virtual List<SensorCollectionDTO> Map(PollingSubscription subscription, PollingWindow window,
        List<NoaaResultDTO> results) =>
        results.Select(result => Collection(subscription,
                DateTime.SpecifyKind(result.Date, DateTimeKind.Utc),
                $"{subscription.StationId}:{result.DataType}:{result.Date:yyyy-MM-dd}", result.Value,
                JsonSerializer.Serialize(result)))
            .ToList();

    protected abstract string Unit { get; }
    protected abstract SensorDomainEnum Domain { get; }

    protected SensorCollectionDTO Collection(PollingSubscription subscription, DateTime recordedAt,
        string sourceEventId, double value, string payload) =>
        new()
        {
            DataSourceId = subscription.DataSourceId,
            TenantId = subscription.TenantId,
            Domain = Domain,
            CollectedAt = recordedAt,
            SourceEventId = sourceEventId,
            Payload = payload,
            Samples =
            [
                new SampleSensorDTO
                {
                    SensorValue = value,
                    Unit = Unit,
                    Latitude = subscription.Latitude,
                    Longitude = subscription.Longitude,
                    RecordedAt = recordedAt
                }
            ]
        };
}
