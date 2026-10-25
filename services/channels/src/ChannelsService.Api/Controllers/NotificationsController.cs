using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationDispatchService _dispatchService;
    private readonly IDeliveryAttemptQueryService _deliveryAttemptQueryService;

    public NotificationsController(
        INotificationDispatchService dispatchService,
        IDeliveryAttemptQueryService deliveryAttemptQueryService)
    {
        _dispatchService = dispatchService;
        _deliveryAttemptQueryService = deliveryAttemptQueryService;
    }

    [HttpGet("{eventId}/attempts")]
    public async Task<IActionResult> GetAttempts(string eventId, CancellationToken cancellationToken)
    {
        var attempts = await _deliveryAttemptQueryService.GetByEventAsync(eventId, cancellationToken);
        return Ok(attempts);
    }

    [HttpPost]
    public async Task<IActionResult> Post(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var result = await _dispatchService.DispatchAsync(notification, cancellationToken);

        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}