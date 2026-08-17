using Microsoft.EntityFrameworkCore;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.Persistence.Entities;

namespace TenantsBilling.Infrastructure.Persistence.Repositories;

public sealed class PostgresPlanRepository : IPlanRepository
{
    private readonly TenantsBillingDbContext _dbContext;

    public PostgresPlanRepository(TenantsBillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(plan);
        await _dbContext.Plans.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Plan MapToDomain(PlanEntity entity)
        => new(
            entity.Id,
            entity.Name,
            entity.MaxEventsPerMonth,
            entity.MaxAlertsPerMonth,
            entity.MaxApiRequestsPerMonth,
            entity.MaxChannelsPerMonth,
            entity.SoftLimitPercentage,
            entity.HardLimitPercentage);

    private static PlanEntity MapToEntity(Plan plan)
        => new()
        {
            Id = plan.Id,
            Name = plan.Name,
            MaxEventsPerMonth = plan.MaxEventsPerMonth,
            MaxAlertsPerMonth = plan.MaxAlertsPerMonth,
            MaxApiRequestsPerMonth = plan.MaxApiRequestsPerMonth,
            MaxChannelsPerMonth = plan.MaxChannelsPerMonth,
            SoftLimitPercentage = plan.SoftLimitPercentage,
            HardLimitPercentage = plan.HardLimitPercentage,
            IsActive = plan.IsActive
        };
}
