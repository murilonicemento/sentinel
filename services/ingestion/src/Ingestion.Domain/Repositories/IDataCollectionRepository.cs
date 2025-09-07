using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Repositories;

public interface IDataCollectionRepository
{
    public Task<Guid> RegisterAsync(DataCollection dataCollection);
}