using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken);
}
