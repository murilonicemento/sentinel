using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Repositories;

public interface IDataSourceRepository
{
    public Task<Guid> Register(DataSource dataSource);
}