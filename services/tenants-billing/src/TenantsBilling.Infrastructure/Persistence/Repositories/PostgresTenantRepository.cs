using Microsoft.EntityFrameworkCore;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.Persistence.Entities;

namespace TenantsBilling.Infrastructure.Persistence.Repositories;

public sealed class PostgresTenantRepository : ITenantRepository
{
    private readonly TenantsBillingDbContext _dbContext;

    public PostgresTenantRepository(TenantsBillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(tenant);
        await _dbContext.Tenants.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenant.Id, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException($"Tenant {tenant.Id} not found.");

        entity.Name = tenant.Name;
        entity.PlanId = tenant.PlanId;
        entity.Status = (int)tenant.Status;
        entity.Region = tenant.Region;
        entity.TimeZone = tenant.TimeZone;
        entity.UpdatedAt = tenant.UpdatedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Tenant MapToDomain(TenantEntity entity)
        => Tenant.Rehydrate(
            entity.Id,
            entity.Name,
            entity.PlanId,
            (TenantStatus)entity.Status,
            entity.Region,
            entity.TimeZone,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static TenantEntity MapToEntity(Tenant tenant)
        => new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            PlanId = tenant.PlanId,
            Status = (int)tenant.Status,
            Region = tenant.Region,
            TimeZone = tenant.TimeZone,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };
}
