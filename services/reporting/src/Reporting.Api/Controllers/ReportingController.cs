using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Security;
using Reporting.Application.Interfaces;
using Reporting.Application.Services;
using Reporting.Domain.Entities;

namespace Reporting.Api.Controllers;

[ApiController]
[Route("api/reporting")]
public class ReportingController : ControllerBase
{
    private readonly ReportingQueryService _queryService;
    private readonly ReportingHealthService _healthService;
    private readonly IReportingEventConsumer _consumer;
    private readonly ReportingEventIngestionService _ingestionService;
    private readonly ReportingReplayService _replayService;
    private readonly TenantAccessEvaluator _tenantAccess;

    public ReportingController(
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
    public async Task<ActionResult<DashboardSummary>> GetSummary(string tenantId, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return Forbid();

        var summary = await _queryService.GetDashboardSummaryAsync(tenantId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("tenants/{tenantId}/analytics")]
    [Authorize(Policy = ReportingPolicies.Read)]
    public async Task<ActionResult<AnalyticsReport>> GetAnalytics(
        string tenantId,
        [FromQuery] string? region,
        [FromQuery] string? eventType,
        [FromQuery] string? severity,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return Forbid();

        var filter = new ReportFilter(tenantId, region, eventType, severity, channel, status, startDate, endDate);
        var analytics = await _queryService.GetAnalyticsReportAsync(tenantId, filter, cancellationToken);
        return Ok(analytics);
    }

    [HttpGet("tenants/{tenantId}/export")]
    [Authorize(Policy = ReportingPolicies.Read)]
    public async Task<IActionResult> Export(
        string tenantId,
        [FromQuery] string? region,
        [FromQuery] string? eventType,
        [FromQuery] string? severity,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return Forbid();

        var filter = new ReportFilter(tenantId, region, eventType, severity, channel, status, startDate, endDate);
        var payload = await _queryService.ExportAsync(tenantId, filter, format, cancellationToken);
        var mediaType = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/csv";
        return File(System.Text.Encoding.UTF8.GetBytes(payload), mediaType, $"reporting-{tenantId}.{format}");
    }

    [HttpGet("health")]
    [Authorize(Policy = ReportingPolicies.Read)]
    public async Task<ActionResult<ServiceHealthSnapshot>> Health(string tenantId, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return Forbid();

        var snapshot = await _healthService.GetHealthSnapshotAsync(tenantId, cancellationToken);
        return Ok(snapshot);
    }

    [HttpPost("events")]
    [Authorize(Policy = ReportingPolicies.Write)]
    public async Task<ActionResult> ReceiveEvent([FromBody] ReportingEvent reportingEvent, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, reportingEvent.TenantId)) return Forbid();

        await _ingestionService.IngestAsync(reportingEvent, cancellationToken);
        return Accepted();
    }

    [HttpPost("events/envelope")]
    [Authorize(Policy = ReportingPolicies.Write)]
    public async Task<ActionResult> ReceiveEnvelope([FromBody] ReportingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, envelope.TenantId)) return Forbid();

        await _consumer.ProcessAsync(envelope, cancellationToken);
        return Accepted();
    }

    [HttpPost("events/replay")]
    [Authorize(Roles = ReportingRoles.Admin)]
    public async Task<ActionResult> ReplayFailedEvents([FromBody] ReplayRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SourceTopic) || string.IsNullOrWhiteSpace(request.Payload))
        {
            return BadRequest("A source topic and payload are required for replay.");
        }

        var result = await _replayService.RequestReplayAsync(
            request.SourceTopic,
            request.Payload,
            request.RequestedBy,
            cancellationToken);

        return Accepted(new
        {
            topic = result.Topic,
            partition = result.Partition,
            offset = result.Offset,
            sourceTopic = result.SourceTopic,
            requestedBy = result.RequestedBy
        });
    }

    public sealed record ReplayRequest(string SourceTopic, string Payload, string? RequestedBy = null);
}
