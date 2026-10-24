using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Infrastructure.InMemory;

public sealed class InMemoryPlanRepository : IPlanRepository
{
    private readonly Dictionary<Guid, Plan> _plans = new();

    public Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_plans.TryGetValue(id, out var plan) ? plan : null);

    public Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        _plans[plan.Id] = plan;
        return Task.CompletedTask;
    }
}
