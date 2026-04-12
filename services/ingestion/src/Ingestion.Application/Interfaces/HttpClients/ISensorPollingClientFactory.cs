namespace Ingestion.Application.Interfaces.HttpClients;

public interface ISensorPollingClientFactory
{
    IEnumerable<ISensorPollingClient> GetAllClients();
}