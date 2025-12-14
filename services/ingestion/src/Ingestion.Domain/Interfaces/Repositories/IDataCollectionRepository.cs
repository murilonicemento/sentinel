using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface IDataCollectionRepository
{
    public Task<Guid> RegisterAsync(DataCollection dataCollection);
}