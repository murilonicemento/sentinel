using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Reporting.Application.Interfaces;
using Reporting.Application.Services;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;
using Reporting.Infrastructure.HostedServices;

namespace Reporting.Api.Controllers;

[ApiController]
[Route("api/reporting")]
public class ReportingController : ControllerBase
{
    private readonly ReportingQueryService _queryService;
    private readonly ReportingHealthService _healthService;
    private readonly IReportingEventConsumer _consumer;
    private readonly IReportingRepository _repository;
    private readonly IConfiguration _configuration;

    public ReportingController(
        ReportingQueryService queryService,
        ReportingHealthService healthService,
        IReportingEventConsumer consumer,
        IReportingRepository repository,
        IConfiguration configuration)
    {
        _queryService = queryService;
        _healthService = healthService;
        _consumer = consumer;
        _repository = repository;
        _configuration = configuration;
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

    [HttpPost("events/replay")]
    public async Task<ActionResult> ReplayFailedEvents([FromBody] ReplayRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SourceTopic) || string.IsNullOrWhiteSpace(request.Payload))
        {
            return BadRequest("A source topic and payload are required for replay.");
        }

        var replayTopic = _configuration["Kafka:ReplayTopic"] ?? "reporting-events-replay";
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        var messageBody = ReportingKafkaConsumerHostedService.BuildReprocessingMessage(
            request.Payload,
            request.SourceTopic,
            string.IsNullOrWhiteSpace(request.RequestedBy) ? "admin" : request.RequestedBy);

        var result = await producer.ProduceAsync(replayTopic, new Message<Null, string>
        {
            Value = messageBody,
            Headers = new Headers
            {
                { "source-topic", System.Text.Encoding.UTF8.GetBytes(request.SourceTopic) },
                { "replay-requested-at", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")) },
                { "replay-requested-by", System.Text.Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(request.RequestedBy) ? "admin" : request.RequestedBy) }
            }
        }, cancellationToken);

        return Accepted(new
        {
            topic = replayTopic,
            partition = result.Partition.Value,
            offset = result.Offset.Value,
            sourceTopic = request.SourceTopic,
            requestedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? "admin" : request.RequestedBy
        });
    }

    public sealed record ReplayRequest(string SourceTopic, string Payload, string? RequestedBy = null);
}
