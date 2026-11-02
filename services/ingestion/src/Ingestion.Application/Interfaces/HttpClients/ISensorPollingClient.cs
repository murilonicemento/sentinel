using Ingestion.Application.DTO;

namespace Ingestion.Application.Interfaces.HttpClients;

public interface ISensorPollingClient
{
    /// <summary>Source name, also the configuration section <c>Polling:&lt;Source&gt;</c>.</summary>
    string Source { get; }

    /// <summary>
    /// What the provider reported for the subscription's area in the window. Each collection carries the provider's
    /// own time and event id (<see cref="SensorCollectionDTO.SourceEventId"/>), so polling the same window again
    /// does not ingest anything twice.
    /// </summary>
    Task<List<SensorCollectionDTO>> FetchAsync(PollingSubscription subscription, PollingWindow window,
        CancellationToken cancellationToken);
}
