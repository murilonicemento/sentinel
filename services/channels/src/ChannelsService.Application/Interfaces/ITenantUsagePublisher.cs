using ChannelsService.Application.DTOs;

namespace ChannelsService.Application.Interfaces;

/// <summary>Reports a tenant's usage to tenants-billing (<c>tenant-usage-events</c>).</summary>
public interface ITenantUsagePublisher
{
    Task PublishAsync(TenantUsageEventDTO usage, CancellationToken cancellationToken);
}
