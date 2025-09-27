using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface IDataSourceRepository
{
    public DataSource? GetByIdAndTenantId(Guid id, Guid tenantId);
    public Task<Guid> RegisterAsync(DataSource dataSource);
}