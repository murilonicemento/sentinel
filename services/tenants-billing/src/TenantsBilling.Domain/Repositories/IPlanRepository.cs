using TenantsBilling.Domain.Aggregates;

namespace TenantsBilling.Domain.Repositories;

public interface IPlanRepository
{
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Plan plan, CancellationToken cancellationToken = default);

    /// <summary>Plans ordered by name, then id.</summary>
    Task<PagedList<Plan>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
