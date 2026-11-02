using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;

namespace TenantsBilling.Domain.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the tenant's status only if the stored status is still <paramref name="expectedStatus"/>, so two
    /// concurrent changes cannot both apply. False when the stored status changed or the tenant no longer exists.
    /// </summary>
    Task<bool> UpdateStatusAsync(Tenant tenant, TenantStatus expectedStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenants ordered by name, then id. <paramref name="tenantIds"/> limits the listing to those tenants (null lists
    /// every tenant); <paramref name="status"/> filters by status.
    /// </summary>
    Task<PagedList<Tenant>> ListAsync(IReadOnlyCollection<Guid>? tenantIds, TenantStatus? status, int page, int pageSize,
        CancellationToken cancellationToken = default);
}
