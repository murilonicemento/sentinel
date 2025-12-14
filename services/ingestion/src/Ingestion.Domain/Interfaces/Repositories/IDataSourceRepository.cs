using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface IDataSourceRepository
{
    public DataSource? GetByIdAndTenantId(Guid id, Guid tenantId);
    public Task<(Guid dataSourceId, Guid tenantId)> RegisterAsync(DataSource dataSource);
}