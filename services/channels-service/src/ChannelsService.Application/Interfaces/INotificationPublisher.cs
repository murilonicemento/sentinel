using ChannelsService.Domain.Models;

namespace ChannelsService.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken);
}
