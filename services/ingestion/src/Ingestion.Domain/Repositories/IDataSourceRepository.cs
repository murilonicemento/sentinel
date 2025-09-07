using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Repositories;

public interface IDataSourceRepository
{
    public DataSource? GetById(Guid id);
    public Task<Guid> RegisterAsync(DataSource dataSource);
}