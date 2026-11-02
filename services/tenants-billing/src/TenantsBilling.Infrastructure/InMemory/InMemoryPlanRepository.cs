using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Infrastructure.InMemory;

public sealed class InMemoryPlanRepository : IPlanRepository
{
    private readonly Dictionary<Guid, Plan> _plans = new();

    public Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_plans) return Task.FromResult(_plans.TryGetValue(id, out var plan) ? plan : null);
    }

    public Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        lock (_plans) _plans[plan.Id] = plan;
        return Task.CompletedTask;
    }

    public Task<PagedList<Plan>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        lock (_plans)
        {
            var ordered = _plans.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id).ToList();
            return Task.FromResult(new PagedList<Plan>(
                ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), ordered.Count));
        }
    }
}
