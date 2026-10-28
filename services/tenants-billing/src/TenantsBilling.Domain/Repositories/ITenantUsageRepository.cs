using TenantsBilling.Domain.Aggregates;

namespace TenantsBilling.Domain.Repositories;

public interface ITenantUsageRepository
{
    Task<TenantUsage?> GetAsync(Guid tenantId, string period, CancellationToken cancellationToken = default);

    /// <summary>False when <paramref name="sourceEventId"/> was already recorded.</summary>
    Task<bool> IsRecordedAsync(string sourceEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the period totals and, when given, marks <paramref name="sourceEventId"/> as recorded in the same
    /// transaction, so a usage event is counted exactly once. Throws <see cref="DuplicateUsageEventException"/> when
    /// another writer recorded the same event first.
    /// </summary>
    Task SaveAsync(TenantUsage usage, string? sourceEventId, CancellationToken cancellationToken = default);
}

public sealed class DuplicateUsageEventException(string sourceEventId)
    : Exception($"Usage event {sourceEventId} was already recorded.");
