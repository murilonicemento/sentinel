using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IChannelDeliveryService _deliveryService;

    public NotificationsController(IChannelDeliveryService deliveryService)
    {
        _deliveryService = deliveryService;
    }

    [HttpPost]
    public async Task<IActionResult> Post(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var result = await _deliveryService.DeliverAsync(notification, cancellationToken);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}
