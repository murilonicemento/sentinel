using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Interfaces;
using Reporting.Application.Services;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Api.Controllers;

[ApiController]
[Route("api/reporting")]
public class ReportingController : ControllerBase
{
    private readonly ReportingQueryService _queryService;
    private readonly ReportingHealthService _healthService;
    private readonly IReportingEventConsumer _consumer;
    private readonly IReportingRepository _repository;

    public ReportingController(ReportingQueryService queryService, ReportingHealthService healthService, IReportingEventConsumer consumer, IReportingRepository repository)
    {
        _queryService = queryService;
        _healthService = healthService;
        _consumer = consumer;
        _repository = repository;
    }

    [HttpGet("tenants/{tenantId}/summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary(string tenantId, CancellationToken cancellationToken)
    {
        var summary = await _queryService.GetDashboardSummaryAsync(tenantId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("tenants/{tenantId}/analytics")]
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
        var filter = new ReportFilter(tenantId, region, eventType, severity, channel, status, startDate, endDate);
        var analytics = await _queryService.GetAnalyticsReportAsync(tenantId, filter, cancellationToken);
        return Ok(analytics);
    }

    [HttpGet("tenants/{tenantId}/export")]
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
        var filter = new ReportFilter(tenantId, region, eventType, severity, channel, status, startDate, endDate);
        var payload = await _queryService.ExportAsync(tenantId, filter, format, cancellationToken);
        var mediaType = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/csv";
        return File(System.Text.Encoding.UTF8.GetBytes(payload), mediaType, $"reporting-{tenantId}.{format}");
    }

    [HttpGet("health")]
    public async Task<ActionResult<ServiceHealthSnapshot>> Health(string tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await _healthService.GetHealthSnapshotAsync(tenantId, cancellationToken);
        return Ok(snapshot);
    }

    [HttpPost("events")]
    public async Task<ActionResult> ReceiveEvent([FromBody] ReportingEvent reportingEvent, CancellationToken cancellationToken)
    {
        await _repository.ProcessEventAsync(reportingEvent, cancellationToken);
        return Accepted();
    }

    [HttpPost("events/envelope")]
    public async Task<ActionResult> ReceiveEnvelope([FromBody] ReportingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        await _consumer.ProcessAsync(envelope, cancellationToken);
        return Accepted();
    }
}
