using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Repositories;

public interface IDataSourceRepository
{
    public DataSource? GetByIdAndTenantId(Guid id, Guid tenantId);
    public Task<Guid> RegisterAsync(DataSource dataSource);
}