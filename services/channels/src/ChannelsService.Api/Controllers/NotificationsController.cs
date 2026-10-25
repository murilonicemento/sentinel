using ChannelsService.Api.Security;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationDispatchService _dispatchService;
    private readonly IDeliveryAttemptQueryService _deliveryAttemptQueryService;
    private readonly TenantAccessEvaluator _tenantAccess;

    public NotificationsController(
        INotificationDispatchService dispatchService,
        IDeliveryAttemptQueryService deliveryAttemptQueryService,
        TenantAccessEvaluator tenantAccess)
    {
        _dispatchService = dispatchService;
        _deliveryAttemptQueryService = deliveryAttemptQueryService;
        _tenantAccess = tenantAccess;
    }

    [HttpGet("{eventId}/attempts")]
    [Authorize(Policy = ChannelsServicePolicies.Read)]
    public async Task<IActionResult> GetAttempts(string eventId, CancellationToken cancellationToken)
    {
        var scope = _tenantAccess.IsServicePrincipal(User)
            ? TenantAccessScope.Platform
            : TenantAccessScope.ForTenants(_tenantAccess.TenantIds(User));
        var attempts = await _deliveryAttemptQueryService.GetByEventAsync(eventId, scope, cancellationToken);
        return Ok(attempts);
    }

    [HttpPost]
    [Authorize(Policy = ChannelsServicePolicies.Write)]
    public async Task<IActionResult> Post(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, notification.TenantId)) return Forbid();

        var result = await _dispatchService.DispatchAsync(notification, cancellationToken);

        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}