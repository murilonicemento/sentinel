using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelsService.Application.Interfaces;

public interface IChannelDeliveryService
{
    Task<DeliveryResult> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken);
}

public interface IRetryPolicyEngine
{
    Task<DeliveryResult> ExecuteAsync(Func<Task<DeliveryResult>> sendFunc, int maxAttempts, CancellationToken cancellationToken);
}

public interface IFallbackExecutor
{
    IReadOnlyList<ChannelType> GetFallbackOrder(NotificationEvent notification, TenantChannelSettings settings);
}

public interface ITenantChannelSettingsProvider
{
    TenantChannelSettings GetSettings(string tenantId);
}

public interface IDeliveryRepository
{
    Task AddAsync(DeliveryAttempt attempt);
    Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId);
}
