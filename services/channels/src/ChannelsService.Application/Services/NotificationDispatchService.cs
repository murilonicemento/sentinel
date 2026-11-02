using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Application.Services;

public sealed class NotificationDispatchService : INotificationDispatchService
{
    private readonly IChannelDeliveryService _deliveryService;
    private readonly IDeadLetterPublisher _deadLetterPublisher;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        IChannelDeliveryService deliveryService,
        IDeadLetterPublisher deadLetterPublisher,
        ILogger<NotificationDispatchService> logger)
    {
        _deliveryService = deliveryService;
        _deadLetterPublisher = deadLetterPublisher;
        _logger = logger;
    }

    public async Task<DeliveryResultDTO> DispatchAsync(NotificationEvent notification,
        CancellationToken cancellationToken)
    {
        var deliveryResult = await _deliveryService.DeliverAsync(notification, cancellationToken);

        if (deliveryResult.Success) return deliveryResult;

        _logger.LogError(
            "Notification event {EventId} failed processing: {Error}",
            notification.EventId,
            deliveryResult.Error);

        await _deadLetterPublisher.PublishAsync(notification, deliveryResult, cancellationToken);

        return deliveryResult;
    }
}
