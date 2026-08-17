using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Infrastructure.InMemory;

public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly Dictionary<Guid, Tenant> _tenants = new();

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_tenants.TryGetValue(id, out var tenant) ? tenant : null);

    public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_tenants.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _tenants[tenant.Id] = tenant;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _tenants[tenant.Id] = tenant;
        return Task.CompletedTask;
    }
}
