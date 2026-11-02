namespace Ingestion.Application.Events;

/// <summary>
/// Payload of <c>tenant-usage-events</c>, consumed by tenants-billing. One per collection: <c>EventsConsumed</c> is
/// the number of events the collection published. <c>EventId</c> is deterministic for the collection, so a retried
/// collection is counted once.
/// </summary>
public record TenantUsageEvent(
    Guid TenantId,
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage,
    Guid EventId)
{
    public const string Topic = "tenant-usage-events";
}
