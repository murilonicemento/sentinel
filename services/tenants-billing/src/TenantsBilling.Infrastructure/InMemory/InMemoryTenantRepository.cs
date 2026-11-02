using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Infrastructure.InMemory;

/// <summary>
/// Stores copies of the tenants, like a database, so a caller's change is visible only after it is saved.
/// </summary>
public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly Dictionary<Guid, Tenant> _tenants = new();
    private readonly object _gate = new();

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_tenants.TryGetValue(id, out var tenant) ? Copy(tenant) : null);
    }

    public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var tenant = _tenants.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(tenant is null ? null : Copy(tenant));
        }
    }

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        lock (_gate) _tenants[tenant.Id] = Copy(tenant);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        lock (_gate) _tenants[tenant.Id] = Copy(tenant);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateStatusAsync(Tenant tenant, TenantStatus expectedStatus,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_tenants.TryGetValue(tenant.Id, out var stored) || stored.Status != expectedStatus)
                return Task.FromResult(false);

            _tenants[tenant.Id] = Tenant.Rehydrate(stored.Id, stored.Name, stored.PlanId, tenant.Status, stored.Region,
                stored.TimeZone, stored.CreatedAt, tenant.UpdatedAt);
            return Task.FromResult(true);
        }
    }

    public Task<PagedList<Tenant>> ListAsync(IReadOnlyCollection<Guid>? tenantIds, TenantStatus? status, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var matching = _tenants.Values
                .Where(x => tenantIds is null || tenantIds.Contains(x.Id))
                .Where(x => status is null || x.Status == status)
                .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id)
                .ToList();

            return Task.FromResult(new PagedList<Tenant>(
                matching.Skip((page - 1) * pageSize).Take(pageSize).Select(Copy).ToList(), matching.Count));
        }
    }

    private static Tenant Copy(Tenant tenant) => Tenant.Rehydrate(tenant.Id, tenant.Name, tenant.PlanId, tenant.Status,
        tenant.Region, tenant.TimeZone, tenant.CreatedAt, tenant.UpdatedAt);
}
