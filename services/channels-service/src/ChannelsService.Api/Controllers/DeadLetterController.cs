using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[Route("api/dlq")]
public class DeadLetterController : ControllerBase
{
    private readonly INotificationPublisher _notificationPublisher;

    public DeadLetterController(INotificationPublisher notificationPublisher)
    {
        _notificationPublisher = notificationPublisher;
    }

    [HttpPost("reprocess")]
    public async Task<IActionResult> Reprocess(NotificationEvent notification, CancellationToken cancellationToken)
    {
        await _notificationPublisher.PublishAsync(notification, cancellationToken);
        return Ok(new { success = true, eventId = notification.EventId });
    }
}
