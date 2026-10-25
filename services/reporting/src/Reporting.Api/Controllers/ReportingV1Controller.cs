using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Contracts;
using Reporting.Api.Security;
using Reporting.Application.Interfaces;
using Reporting.Application.Models;
using Reporting.Application.Services;
using Reporting.Domain.Entities;

namespace Reporting.Api.Controllers;

public sealed record ReportingEventRequest
{
    [Required] public string EventId { get; init; } = string.Empty;
    [Required] public string EventType { get; init; } = string.Empty;
    [Required] public string TenantId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? Timestamp { get; init; }

    public ReportingEvent ToEvent() =>
        new(EventId, EventType, TenantId, Region, Severity, Channel, RiskScore, Status, Timestamp);
}

public sealed record ReportingEnvelopeRequest
{
    [Required] public string EventId { get; init; } = string.Empty;
    [Required] public string EventType { get; init; } = string.Empty;
    [Required] public string TenantId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public string Status { get; init; } = string.Empty;
    [Required] public DateTime Timestamp { get; init; }
    [Required] public string Source { get; init; } = string.Empty;

    public ReportingEventEnvelope ToEnvelope() =>
        new(EventId, EventType, TenantId, Region, Severity, Channel, RiskScore, Status, Timestamp, Source);
}

public sealed record ReplayRequest
{
    [Required] public string SourceTopic { get; init; } = string.Empty;
    [Required] public string Payload { get; init; } = string.Empty;
    public string? RequestedBy { get; init; }
}

public sealed record AcceptedEventDTO(string EventId);

/// <summary>Reporting API v1. Timestamps are ISO 8601 UTC; date filters are inclusive instants.</summary>
[ApiController]
[Route(BasePath)]
public class ReportingV1Controller : ControllerBase
{
    public const string BasePath = "api/v1/reporting";

    private static readonly string[] ExportFormats = ["csv", "json"];

    private readonly ReportingQueryService _queryService;
    private readonly ReportingHealthService _healthService;
    private readonly IReportingEventConsumer _consumer;
    private readonly ReportingEventIngestionService _ingestionService;
    private readonly ReportingReplayService _replayService;
    private readonly TenantAccessEvaluator _tenantAccess;

    public ReportingV1Controller(
        ReportingQueryService queryService,
        ReportingHealthService healthService,
        IReportingEventConsumer consumer,
        ReportingEventIngestionService ingestionService,
        ReportingReplayService replayService,
        TenantAccessEvaluator tenantAccess)
    {
        _queryService = queryService;
        _healthService = healthService;
        _consumer = consumer;
        _ingestionService = ingestionService;
        _replayService = replayService;
        _tenantAccess = tenantAccess;
    }

    [HttpGet("tenants/{tenantId}/summary")]
    [Authorize(Policy = ReportingPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<DashboardSummaryDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetSummary(string tenantId, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return TenantAccessDenied();

        var summary = await _queryService.GetDashboardSummaryAsync(tenantId, cancellationToken);
        return Ok(Envelope(StatusCodes.Status200OK, DashboardSummaryDTO.From(summary)));
    }

    [HttpGet("tenants/{tenantId}/analytics")]
    [Authorize(Policy = ReportingPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<AnalyticsReportDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetAnalytics(string tenantId, [FromQuery] ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return TenantAccessDenied();
        if (InvalidDateRange(query) is { } invalid) return invalid;

        var report = await _queryService.GetAnalyticsReportAsync(tenantId, query.ToFilter(tenantId), cancellationToken);
        return Ok(Envelope(StatusCodes.Status200OK, AnalyticsReportDTO.From(report)));
    }

    /// <summary>Exports the tenant's events as CSV (default) or JSON.</summary>
    [HttpGet("tenants/{tenantId}/exports")]
    [Authorize(Policy = ReportingPolicies.Read)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv", "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Export(string tenantId, [FromQuery] ReportFilterQuery query,
        [FromQuery] string? format, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return TenantAccessDenied();

        var resolvedFormat = string.IsNullOrWhiteSpace(format) ? "csv" : format.ToLowerInvariant();
        if (!ExportFormats.Contains(resolvedFormat))
            return Invalid(new Dictionary<string, string[]> { ["format"] = ["The format must be csv or json."] });
        if (InvalidDateRange(query) is { } invalid) return invalid;

        var payload = await _queryService.ExportAsync(tenantId, query.ToFilter(tenantId), resolvedFormat,
            cancellationToken);
        var mediaType = resolvedFormat == "json" ? "application/json" : "text/csv";
        return File(Encoding.UTF8.GetBytes(payload), mediaType, $"reporting-{tenantId}.{resolvedFormat}");
    }

    [HttpGet("tenants/{tenantId}/processing-status")]
    [Authorize(Policy = ReportingPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<ProcessingStatusDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetProcessingStatus(string tenantId, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return TenantAccessDenied();

        var snapshot = await _healthService.GetHealthSnapshotAsync(tenantId, cancellationToken);
        return Ok(Envelope(StatusCodes.Status200OK, ProcessingStatusDTO.From(snapshot)));
    }

    [HttpPost("events")]
    [Authorize(Policy = ReportingPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<AcceptedEventDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> ReceiveEvent([FromBody] ReportingEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, request.TenantId)) return TenantAccessDenied();

        await _ingestionService.IngestAsync(request.ToEvent(), cancellationToken);
        return Accepted(Envelope(StatusCodes.Status202Accepted, new AcceptedEventDTO(request.EventId)));
    }

    [HttpPost("events/envelope")]
    [Authorize(Policy = ReportingPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<AcceptedEventDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> ReceiveEnvelope([FromBody] ReportingEnvelopeRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, request.TenantId)) return TenantAccessDenied();

        await _consumer.ProcessAsync(request.ToEnvelope(), cancellationToken);
        return Accepted(Envelope(StatusCodes.Status202Accepted, new AcceptedEventDTO(request.EventId)));
    }

    /// <summary>Publishes a payload to the replay topic. Restricted to Reporting.Admin.</summary>
    [HttpPost("events/replay")]
    [Authorize(Roles = ReportingRoles.Admin)]
    [ProducesResponseType<ResponseBaseDTO<ReplayResultDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Replay([FromBody] ReplayRequest request, CancellationToken cancellationToken)
    {
        var result = await _replayService.RequestReplayAsync(request.SourceTopic, request.Payload,
            request.RequestedBy, cancellationToken);
        return Accepted(Envelope(StatusCodes.Status202Accepted, ReplayResultDTO.From(result)));
    }

    private IActionResult? InvalidDateRange(ReportFilterQuery query) =>
        query.StartDate is { } start && query.EndDate is { } end && start > end
            ? Invalid(new Dictionary<string, string[]> { ["startDate"] = ["The startDate must not be after endDate."] })
            : null;

    private IActionResult Invalid(Dictionary<string, string[]> errors) =>
        new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationError,
            "One or more fields are invalid.", errors));

    private IActionResult TenantAccessDenied() =>
        new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
            "The authenticated identity is not authorized for the requested tenant."));

    private static ResponseBaseDTO<T> Envelope<T>(int statusCode, T data) =>
        new() { StatusCode = statusCode, Success = true, Data = data };
}

public sealed record ReportFilterQuery
{
    public string? Region { get; init; }
    public string? EventType { get; init; }
    public string? Severity { get; init; }
    public string? Channel { get; init; }
    public string? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }

    public ReportFilter ToFilter(string tenantId) =>
        new(tenantId, Region, EventType, Severity, Channel, Status, StartDate, EndDate);
}
