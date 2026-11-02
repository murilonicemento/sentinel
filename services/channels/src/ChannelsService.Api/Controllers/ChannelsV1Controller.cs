using System.ComponentModel.DataAnnotations;
using ChannelsService.Api.Contracts;
using ChannelsService.Api.Security;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsService.Api.Controllers;

public sealed record NotificationMessageRequest
{
    [Required] public string Title { get; init; } = string.Empty;
    [Required] public string Body { get; init; } = string.Empty;
}

public sealed record SendNotificationRequest
{
    [Required] public string EventId { get; init; } = string.Empty;
    [Required] public string TenantId { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? EventType { get; init; }
    public string? CorrelationId { get; init; }
    public bool FallbackEnabled { get; init; } = true;
    public NotificationPriorityEnum Priority { get; init; } = NotificationPriorityEnum.Medium;
    [Required] public NotificationMessageRequest Message { get; init; } = new();
    [MinLength(1)] public List<ChannelTypeEnum> Channels { get; init; } = [];
    public Dictionary<string, string>? Metadata { get; init; }

    public NotificationEvent ToEvent() => new()
    {
        EventId = EventId,
        TenantId = TenantId,
        UserId = UserId ?? string.Empty,
        EventType = EventType ?? string.Empty,
        CorrelationId = CorrelationId ?? string.Empty,
        FallbackEnabled = FallbackEnabled,
        PriorityEnum = Priority,
        Message = new NotificationMessage { Title = Message.Title, Body = Message.Body },
        Channels = Channels,
        Metadata = Metadata
    };
}

public sealed record ReprocessResultDTO(string EventId);

[ApiController]
[Route(BasePath)]
public class ChannelsV1Controller : ControllerBase
{
    public const string BasePath = "api/v1/channels";

    private readonly INotificationDispatchService _dispatchService;
    private readonly IDeliveryAttemptQueryService _attemptQueryService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TenantAccessEvaluator _tenantAccess;

    public ChannelsV1Controller(
        INotificationDispatchService dispatchService,
        IDeliveryAttemptQueryService attemptQueryService,
        INotificationPublisher notificationPublisher,
        TenantAccessEvaluator tenantAccess)
    {
        _dispatchService = dispatchService;
        _attemptQueryService = attemptQueryService;
        _notificationPublisher = notificationPublisher;
        _tenantAccess = tenantAccess;
    }

    /// <summary>
    /// Delivers a notification through the requested channels. When every provider fails, the notification is sent
    /// to the dead-letter topic and 502 is returned.
    /// </summary>
    [HttpPost("notifications")]
    [Authorize(Policy = ChannelsServicePolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<DeliveryResultDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, request.TenantId)) return TenantAccessDenied();

        var result = await _dispatchService.DispatchAsync(request.ToEvent(), cancellationToken);

        return result.Success
            ? Ok(Envelope(StatusCodes.Status200OK, result))
            : new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status502BadGateway,
                "NOTIFICATION_DELIVERY_FAILED",
                "The notification could not be delivered and was moved to the dead-letter topic."));
    }

    /// <summary>Delivery attempts of a notification in chronological order, limited to the caller's tenants.</summary>
    [HttpGet("notifications/{eventId}/attempts")]
    [Authorize(Policy = ChannelsServicePolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<DeliveryAttemptDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetAttempts(string eventId, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest,
                ApiProblems.ValidationError, "One or more fields are invalid.", errors));

        var scope = _tenantAccess.IsServicePrincipal(User)
            ? TenantAccessScope.Platform
            : TenantAccessScope.ForTenants(_tenantAccess.TenantIds(User));
        var result = await _attemptQueryService.GetPageByEventAsync(eventId, scope, resolvedPage, resolvedPageSize,
            cancellationToken);
        return Ok(Envelope(StatusCodes.Status200OK, result));
    }

    /// <summary>Publishes a dead-lettered notification back to the notification topic.</summary>
    [HttpPost("dead-letters/reprocess")]
    [Authorize(Policy = ChannelsServicePolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<ReprocessResultDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Reprocess([FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, request.TenantId)) return TenantAccessDenied();

        await _notificationPublisher.PublishAsync(request.ToEvent(), cancellationToken);
        return Accepted(Envelope(StatusCodes.Status202Accepted, new ReprocessResultDTO(request.EventId)));
    }

    private IActionResult TenantAccessDenied() =>
        new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
            "The authenticated identity is not authorized for the requested tenant."));

    private static ResponseBaseDTO<T> Envelope<T>(int statusCode, T data) =>
        new() { StatusCode = statusCode, Success = true, Data = data };
}
