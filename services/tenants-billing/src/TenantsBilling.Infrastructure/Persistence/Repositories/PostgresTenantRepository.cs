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

    public async Task<bool> UpdateStatusAsync(Tenant tenant, TenantStatus expectedStatus,
        CancellationToken cancellationToken = default)
    {
        var expected = (int)expectedStatus;
        var updated = await _dbContext.Tenants
            .Where(x => x.Id == tenant.Id && x.Status == expected)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, (int)tenant.Status)
                .SetProperty(x => x.UpdatedAt, tenant.UpdatedAt), cancellationToken);

        return updated == 1;
    }

    public async Task<PagedList<Tenant>> ListAsync(IReadOnlyCollection<Guid>? tenantIds, TenantStatus? status, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tenants.AsNoTracking();
        if (tenantIds is not null) query = query.Where(x => tenantIds.Contains(x.Id));
        if (status is not null)
        {
            var value = (int)status.Value;
            query = query.Where(x => x.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Tenant>(entities.Select(MapToDomain).ToList(), total);
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
