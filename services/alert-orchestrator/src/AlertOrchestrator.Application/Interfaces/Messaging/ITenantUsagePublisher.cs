using AlertOrchestrator.Application.Events;

namespace AlertOrchestrator.Application.Interfaces.Messaging;

/// <summary>Reports a tenant's usage to tenants-billing (<c>tenant-usage-events</c>).</summary>
public interface ITenantUsagePublisher
{
    Task PublishAsync(TenantUsageEvent usage, CancellationToken cancellationToken = default);
}
