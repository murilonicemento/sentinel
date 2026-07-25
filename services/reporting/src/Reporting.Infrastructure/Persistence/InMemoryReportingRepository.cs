using System.Collections.Concurrent;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Infrastructure.Persistence;

public sealed class InMemoryReportingRepository : IReportingRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<ReportingEvent>> _tenantEvents = new(StringComparer.OrdinalIgnoreCase);

    public Task ProcessEventAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportingEvent);

        if (string.IsNullOrWhiteSpace(reportingEvent.TenantId))
        {
            throw new ArgumentException("A tenant identifier is required.", nameof(reportingEvent));
        }

        var tenantBucket = _tenantEvents.GetOrAdd(reportingEvent.TenantId, _ => new ConcurrentBag<ReportingEvent>());
        tenantBucket.Add(reportingEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ReportingEvent>> GetEventsForTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(Array.Empty<ReportingEvent>());
        }

        if (_tenantEvents.TryGetValue(tenantId, out var bucket))
        {
            return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(bucket.ToArray());
        }

        return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(Array.Empty<ReportingEvent>());
    }
}
