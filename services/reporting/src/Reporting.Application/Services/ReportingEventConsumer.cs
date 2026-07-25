using Microsoft.Extensions.Logging;
using Reporting.Application.Interfaces;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Application.Services;

public sealed class ReportingEventConsumer : IReportingEventConsumer
{
    private readonly IReportingRepository _repository;
    private readonly IReportingEventNormalizer _normalizer;
    private readonly ILogger<ReportingEventConsumer> _logger;

    public ReportingEventConsumer(
        IReportingRepository repository,
        IReportingEventNormalizer normalizer,
        ILogger<ReportingEventConsumer> logger)
    {
        _repository = repository;
        _normalizer = normalizer;
        _logger = logger;
    }

    public async Task ProcessAsync(ReportingEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var normalized = _normalizer.Normalize(envelope);
        if (!normalized.IsValid)
        {
            _logger.LogWarning(
                "Validation failed for reporting event {EventId}: {Errors}",
                envelope?.EventId ?? string.Empty,
                string.Join("; ", normalized.Errors));

            throw new InvalidOperationException($"Reporting event validation failed: {string.Join("; ", normalized.Errors)}");
        }

        var normalizedEnvelope = normalized.NormalizedEnvelope!;
        var eventToPersist = new ReportingEvent(
            normalizedEnvelope.EventId,
            normalizedEnvelope.EventType,
            normalizedEnvelope.TenantId,
            normalizedEnvelope.Region,
            normalizedEnvelope.Severity,
            normalizedEnvelope.Channel,
            normalizedEnvelope.RiskScore,
            normalizedEnvelope.Status,
            normalizedEnvelope.Timestamp);

        _logger.LogInformation(
            "Persisting reporting event. EventId={EventId}, TenantId={TenantId}, EventType={EventType}",
            eventToPersist.EventId,
            eventToPersist.TenantId,
            eventToPersist.EventType);

        await _repository.ProcessEventAsync(eventToPersist, cancellationToken);
    }
}
