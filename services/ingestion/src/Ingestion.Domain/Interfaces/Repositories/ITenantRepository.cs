using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface ITenantRepository
{
    public Task<bool> ExistsAsync(Guid tenantId);
    public Task<Tenant?> GetByIdAsync(Guid tenantId);
    public Task<Tenant> RegisterAsync(Tenant tenant);
}