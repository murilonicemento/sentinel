using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IChannelDeliveryService
{
    Task<DeliveryResult> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken);
}