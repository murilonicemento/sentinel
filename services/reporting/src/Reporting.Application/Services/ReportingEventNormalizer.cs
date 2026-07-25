using Reporting.Application.Interfaces;
using Reporting.Application.Models;
using Reporting.Domain.Entities;

namespace Reporting.Application.Services;

public sealed class ReportingEventNormalizer : IReportingEventNormalizer
{
    private static readonly string[] RequiredProperties =
    {
        nameof(ReportingEventEnvelope.EventId),
        nameof(ReportingEventEnvelope.EventType),
        nameof(ReportingEventEnvelope.TenantId),
        nameof(ReportingEventEnvelope.Region),
        nameof(ReportingEventEnvelope.Severity),
        nameof(ReportingEventEnvelope.Channel),
        nameof(ReportingEventEnvelope.Status)
    };

    public ReportingEventNormalizationResult Normalize(ReportingEventEnvelope envelope)
    {
        if (envelope is null)
        {
            return new ReportingEventNormalizationResult(false, new[] { "Envelope payload cannot be null." }, null);
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(envelope.EventId))
        {
            errors.Add("EventId is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.EventType))
        {
            errors.Add("EventType is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.TenantId))
        {
            errors.Add("TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Region))
        {
            errors.Add("Region is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Severity))
        {
            errors.Add("Severity is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Channel))
        {
            errors.Add("Channel is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Status))
        {
            errors.Add("Status is required.");
        }

        if (envelope.RiskScore < 0 || envelope.RiskScore > 1)
        {
            errors.Add("RiskScore must be between 0 and 1.");
        }

        var normalizedEvent = new ReportingEventEnvelope(
            envelope.EventId.Trim(),
            envelope.EventType.Trim(),
            envelope.TenantId.Trim(),
            envelope.Region.Trim().ToUpperInvariant(),
            envelope.Severity.Trim().ToUpperInvariant(),
            envelope.Channel.Trim().ToLowerInvariant(),
            envelope.RiskScore,
            envelope.Status.Trim().ToUpperInvariant(),
            envelope.Timestamp == default ? DateTime.UtcNow : envelope.Timestamp,
            string.IsNullOrWhiteSpace(envelope.Source) ? "unknown" : envelope.Source.Trim());

        if (errors.Count > 0)
        {
            return new ReportingEventNormalizationResult(false, errors.ToArray(), normalizedEvent);
        }

        return new ReportingEventNormalizationResult(true, Array.Empty<string>(), normalizedEvent);
    }
}
