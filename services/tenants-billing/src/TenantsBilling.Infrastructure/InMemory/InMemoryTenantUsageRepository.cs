using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Infrastructure.InMemory;

/// <summary>Single-instance fallback used when Postgres is disabled; totals do not survive a restart.</summary>
public sealed class InMemoryTenantUsageRepository : ITenantUsageRepository
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _recordedEvents = [];
    private readonly Dictionary<(Guid TenantId, string Period), TenantUsage> _usage = new();

    public Task<TenantUsage?> GetAsync(Guid tenantId, string period, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_usage.TryGetValue((tenantId, period), out var usage)) return Task.FromResult<TenantUsage?>(null);
            // Returns a copy, like a database read, so a failed save does not leave partial totals behind.
            return Task.FromResult<TenantUsage?>(TenantUsage.Rehydrate(usage.TenantId, usage.Period,
                usage.EventsConsumed, usage.AlertsTriggered, usage.ApiRequests, usage.ChannelUsage, usage.QuotaStatus,
                usage.UpdatedAt));
        }
    }

    public Task<bool> IsRecordedAsync(string sourceEventId, CancellationToken cancellationToken = default)
    {
        lock (_lock) return Task.FromResult(_recordedEvents.Contains(sourceEventId));
    }

    public Task SaveAsync(TenantUsage usage, string? sourceEventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (sourceEventId is not null && !_recordedEvents.Add(sourceEventId))
                throw new DuplicateUsageEventException(sourceEventId);
            _usage[(usage.TenantId, usage.Period)] = usage;
        }

        return Task.CompletedTask;
    }
}
