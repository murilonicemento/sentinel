using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IChannelDeliveryService _deliveryService;
    private readonly IDeadLetterPublisher _deadLetterPublisher;
    private readonly IDeliveryRepository _deliveryRepository;

    public NotificationsController(
        IChannelDeliveryService deliveryService,
        IDeadLetterPublisher deadLetterPublisher,
        IDeliveryRepository deliveryRepository)
    {
        _deliveryService = deliveryService;
        _deadLetterPublisher = deadLetterPublisher;
        _deliveryRepository = deliveryRepository;
    }

    [HttpGet("{eventId}/attempts")]
    public async Task<IActionResult> GetAttempts(string eventId, CancellationToken cancellationToken)
    {
        var attempts = await _deliveryRepository.GetByEventAsync(eventId);
        return Ok(attempts);
    }

    [HttpPost]
    public async Task<IActionResult> Post(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var result = await _deliveryService.DeliverAsync(notification, cancellationToken);

        if (result.Success)
            return Ok(result);

        await _deadLetterPublisher.PublishAsync(notification, result, cancellationToken);
        return StatusCode(500, result);
    }
}