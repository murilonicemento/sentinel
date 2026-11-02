using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Interfaces;

public interface INotificationDispatchService
{
    /// <summary>
    /// Delivers the notification and, when delivery fails, publishes it to the dead-letter topic.
    /// </summary>
    Task<DeliveryResultDTO> DispatchAsync(NotificationEvent notification, CancellationToken cancellationToken);
}
