using Ingestion.Application.DTO;

namespace Ingestion.Application.Interfaces.HttpClients;

public interface ISensorPollingClient
{
    Task<List<SensorCollectionDTO>> FetchAsync(CancellationToken cancellationToken);
}