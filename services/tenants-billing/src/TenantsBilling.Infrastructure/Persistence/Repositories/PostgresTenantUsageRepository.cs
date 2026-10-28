using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.Persistence.Entities;

namespace TenantsBilling.Infrastructure.Persistence.Repositories;

public sealed class PostgresTenantUsageRepository : ITenantUsageRepository
{
    private const string UniqueViolation = "23505";

    private readonly TenantsBillingDbContext _dbContext;

    public PostgresTenantUsageRepository(TenantsBillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantUsage?> GetAsync(Guid tenantId, string period, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TenantUsage
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Period == period, cancellationToken);

        return entity is null
            ? null
            : TenantUsage.Rehydrate(entity.TenantId, entity.Period, entity.EventsConsumed, entity.AlertsTriggered,
                entity.ApiRequests, entity.ChannelUsage, (TenantQuotaStatus)entity.QuotaStatus, entity.UpdatedAt);
    }

    public Task<bool> IsRecordedAsync(string sourceEventId, CancellationToken cancellationToken = default) =>
        _dbContext.RecordedUsageEvents.AnyAsync(x => x.SourceEventId == sourceEventId, cancellationToken);

    public async Task SaveAsync(TenantUsage usage, string? sourceEventId, CancellationToken cancellationToken = default)
    {
        // The entity loaded by GetAsync is tracked, so its xmin is the original version for the update.
        var entity = _dbContext.TenantUsage.Local
            .FirstOrDefault(x => x.TenantId == usage.TenantId && x.Period == usage.Period);
        if (entity is null)
        {
            entity = new TenantUsageEntity { TenantId = usage.TenantId, Period = usage.Period };
            _dbContext.TenantUsage.Add(entity);
        }

        entity.EventsConsumed = usage.EventsConsumed;
        entity.AlertsTriggered = usage.AlertsTriggered;
        entity.ApiRequests = usage.ApiRequests;
        entity.ChannelUsage = usage.ChannelUsage;
        entity.QuotaStatus = (int)usage.QuotaStatus;
        entity.UpdatedAt = usage.UpdatedAt;

        if (sourceEventId is not null)
            _dbContext.RecordedUsageEvents.Add(new RecordedUsageEventEntity
            {
                SourceEventId = sourceEventId,
                TenantId = usage.TenantId,
                Period = usage.Period,
                RecordedAt = usage.UpdatedAt
            });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: UniqueViolation,
            TableName: "recorded_usage_events"
        })
        {
            throw new DuplicateUsageEventException(sourceEventId!);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: UniqueViolation, TableName: "tenant_usage"
        })
        {
            // Two writers started the same period at once; reprocessing reads the row the other one created.
            throw new DbUpdateConcurrencyException("The billing period was created concurrently.", exception);
        }
    }
}
