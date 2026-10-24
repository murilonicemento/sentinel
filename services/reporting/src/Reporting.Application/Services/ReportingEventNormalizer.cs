using Reporting.Application.Interfaces;
using Reporting.Application.Models;
using Reporting.Domain.Entities;

namespace Reporting.Application.Services;

public sealed class ReportingEventNormalizer : IReportingEventNormalizer
{
    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AlertTriggered",
        "AlertDispatched",
        "NotificationSent",
        "NotificationFailed",
        "RiskUpdated",
        "SensorEventDetected",
        "SensorOffline",
        "SensorRecovered"
    };

    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOW",
        "MEDIUM",
        "HIGH",
        "CRITICAL"
    };

    private static readonly HashSet<string> AllowedChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "sms",
        "email",
        "push",
        "webhook"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING",
        "PROCESSING",
        "SUCCESS",
        "FAILED",
        "REJECTED"
    };

    private static void AddEventSpecificValidationErrors(ReportingEventEnvelope envelope, ICollection<string> errors)
    {
        var eventType = envelope.EventType?.Trim();
        var severity = envelope.Severity?.Trim();
        var channel = envelope.Channel?.Trim();
        var riskScore = envelope.RiskScore;

        if (string.Equals(eventType, "AlertTriggered", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(severity, "HIGH", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(severity, "CRITICAL", StringComparison.OrdinalIgnoreCase)
                || riskScore < 0.6)
            {
                errors.Add("AlertTriggered events must have severity HIGH or CRITICAL and riskScore >= 0.6.");
            }
        }

        if (string.Equals(eventType, "NotificationSent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "NotificationFailed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(channel) && !AllowedChannels.Contains(channel.ToLowerInvariant()))
            {
                errors.Add("Notification events must use one of sms, email, push, webhook.");
            }
        }
    }

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
        else if (!AllowedEventTypes.Contains(envelope.EventType.Trim()))
        {
            errors.Add("EventType is not recognized.");
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
        else if (!AllowedSeverities.Contains(envelope.Severity.Trim().ToUpperInvariant()))
        {
            errors.Add("Severity must be one of LOW, MEDIUM, HIGH, CRITICAL.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Channel))
        {
            errors.Add("Channel is required.");
        }
        else if (!AllowedChannels.Contains(envelope.Channel.Trim().ToLowerInvariant()))
        {
            errors.Add("Channel must be one of sms, email, push, webhook.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Status))
        {
            errors.Add("Status is required.");
        }
        else if (!AllowedStatuses.Contains(envelope.Status.Trim().ToUpperInvariant()))
        {
            errors.Add("Status must be one of PENDING, PROCESSING, SUCCESS, FAILED, REJECTED.");
        }

        if (envelope.RiskScore < 0 || envelope.RiskScore > 1)
        {
            errors.Add("RiskScore must be between 0 and 1.");
        }

        AddEventSpecificValidationErrors(envelope, errors);

        var normalizedEventType = envelope.EventType.Trim();
        var normalizedSeverity = envelope.Severity.Trim();
        var normalizedChannel = envelope.Channel.Trim();
        var normalizedStatus = envelope.Status.Trim();

        var normalizedEvent = new ReportingEventEnvelope(
            envelope.EventId.Trim(),
            normalizedEventType,
            envelope.TenantId.Trim(),
            envelope.Region.Trim().ToUpperInvariant(),
            normalizedSeverity.ToUpperInvariant(),
            normalizedChannel.ToLowerInvariant(),
            envelope.RiskScore,
            normalizedStatus.ToUpperInvariant(),
            envelope.Timestamp == default ? DateTime.UtcNow : envelope.Timestamp,
            string.IsNullOrWhiteSpace(envelope.Source) ? "unknown" : envelope.Source.Trim());

        if (errors.Count > 0)
        {
            return new ReportingEventNormalizationResult(false, errors.ToArray(), normalizedEvent);
        }

        return new ReportingEventNormalizationResult(true, Array.Empty<string>(), normalizedEvent);
    }
}
