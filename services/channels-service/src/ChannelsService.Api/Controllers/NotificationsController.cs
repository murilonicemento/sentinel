using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IChannelDeliveryService _deliveryService;
    private readonly IDeliveryRepository _deliveryRepository;

    public NotificationsController(
        IChannelDeliveryService deliveryService,
        IDeliveryRepository deliveryRepository)
    {
        _deliveryService = deliveryService;
        _deliveryRepository = deliveryRepository;
    }

    [HttpPost]
    public async Task<IActionResult> Post(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var result = await _deliveryService.DeliverAsync(notification, cancellationToken);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("{eventId}/attempts")]
    public async Task<IActionResult> GetAttempts(string eventId, CancellationToken cancellationToken)
    {
        var attempts = await _deliveryRepository.GetByEventAsync(eventId);
        return Ok(attempts);
    }
}
