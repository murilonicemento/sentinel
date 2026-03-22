using Ingestion.Application.Interfaces.HttpClients;

namespace Ingestion.Infrastructure.Write.HttpClients;

public interface ISensorPollingClientFactory
{
    IEnumerable<ISensorPollingClient> GetAllClients();
}