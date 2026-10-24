using Reporting.Application.Models;
using Reporting.Application.Services;
using Reporting.Domain.Entities;

namespace Reporting.UnitTests;

public class ReportingEventNormalizerTests
{
    [Fact]
    public void Normalize_WithValidEnvelope_ReturnsSuccess()
    {
        var normalizer = new ReportingEventNormalizer();
        var envelope = new ReportingEventEnvelope(
            "evt-1",
            "AlertTriggered",
            "tenant-a",
            "north",
            "high",
            "SMS",
            0.85,
            "success",
            DateTime.UtcNow,
            "alert-orchestrator");

        var result = normalizer.Normalize(envelope);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.NormalizedEnvelope);
        Assert.Equal("NORTH", result.NormalizedEnvelope.Region);
        Assert.Equal("HIGH", result.NormalizedEnvelope.Severity);
        Assert.Equal("sms", result.NormalizedEnvelope.Channel);
        Assert.Equal("SUCCESS", result.NormalizedEnvelope.Status);
    }

    [Fact]
    public void Normalize_WithMissingRequiredFields_ReturnsErrors()
    {
        var normalizer = new ReportingEventNormalizer();
        var envelope = new ReportingEventEnvelope(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            -0.1,
            string.Empty,
            default,
            string.Empty);

        var result = normalizer.Normalize(envelope);

        Assert.False(result.IsValid);
        Assert.Contains("EventId is required.", result.Errors);
        Assert.Contains("EventType is required.", result.Errors);
        Assert.Contains("TenantId is required.", result.Errors);
        Assert.Contains("Region is required.", result.Errors);
        Assert.Contains("Severity is required.", result.Errors);
        Assert.Contains("Channel is required.", result.Errors);
        Assert.Contains("Status is required.", result.Errors);
        Assert.Contains("RiskScore must be between 0 and 1.", result.Errors);
    }

    [Fact]
    public void Normalize_WithUnsupportedEventType_ReturnsErrors()
    {
        var normalizer = new ReportingEventNormalizer();
        var envelope = new ReportingEventEnvelope(
            "evt-2",
            "UnknownEvent",
            "tenant-a",
            "north",
            "high",
            "sms",
            0.7,
            "SUCCESS",
            DateTime.UtcNow,
            "ingestion");

        var result = normalizer.Normalize(envelope);

        Assert.False(result.IsValid);
        Assert.Contains("EventType is not recognized.", result.Errors);
    }

    [Fact]
    public void Normalize_WithUnsupportedSeverityOrChannel_ReturnsErrors()
    {
        var normalizer = new ReportingEventNormalizer();
        var envelope = new ReportingEventEnvelope(
            "evt-3",
            "NotificationSent",
            "tenant-a",
            "north",
            "urgent",
            "fax",
            0.4,
            "SUCCESS",
            DateTime.UtcNow,
            "channels");

        var result = normalizer.Normalize(envelope);

        Assert.False(result.IsValid);
        Assert.Contains("Severity must be one of LOW, MEDIUM, HIGH, CRITICAL.", result.Errors);
        Assert.Contains("Channel must be one of sms, email, push, webhook.", result.Errors);
    }

    [Fact]
    public void Normalize_WithAlertTriggeredLowSeverity_ReturnsEventSpecificError()
    {
        var normalizer = new ReportingEventNormalizer();
        var envelope = new ReportingEventEnvelope(
            "evt-4",
            "AlertTriggered",
            "tenant-a",
            "north",
            "low",
            "sms",
            0.3,
            "SUCCESS",
            DateTime.UtcNow,
            "alert-orchestrator");

        var result = normalizer.Normalize(envelope);

        Assert.False(result.IsValid);
        Assert.Contains("AlertTriggered events must have severity HIGH or CRITICAL and riskScore >= 0.6.", result.Errors);
    }

    [Fact]
    public void Normalize_WithNotificationFailedFromUnsupportedChannel_ReturnsEventSpecificError()
    {
        var normalizer = new ReportingEventNormalizer();
        var envelope = new ReportingEventEnvelope(
            "evt-5",
            "NotificationFailed",
            "tenant-a",
            "south",
            "MEDIUM",
            "fax",
            0.8,
            "FAILED",
            DateTime.UtcNow,
            "channels-service");

        var result = normalizer.Normalize(envelope);

        Assert.False(result.IsValid);
        Assert.Contains("Notification events must use one of sms, email, push, webhook.", result.Errors);
    }
}
