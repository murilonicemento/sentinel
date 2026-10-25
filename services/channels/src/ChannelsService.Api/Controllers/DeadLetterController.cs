using ChannelsService.Api.Contracts;
using ChannelsService.Api.Security;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[DeprecatedRoute("/" + ChannelsV1Controller.BasePath + "/dead-letters/reprocess")]
[Route("api/dlq")]
public class DeadLetterController : ControllerBase
{
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TenantAccessEvaluator _tenantAccess;

    public DeadLetterController(INotificationPublisher notificationPublisher, TenantAccessEvaluator tenantAccess)
    {
        _notificationPublisher = notificationPublisher;
        _tenantAccess = tenantAccess;
    }

    [HttpPost("reprocess")]
    [Authorize(Policy = ChannelsServicePolicies.Write)]
    public async Task<IActionResult> Reprocess(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, notification.TenantId)) return Forbid();

        await _notificationPublisher.PublishAsync(notification, cancellationToken);
        return Ok(new { success = true, eventId = notification.EventId });
    }
}
