using Reporting.Application.Services;
using Reporting.Infrastructure.HostedServices;

namespace Reporting.UnitTests;

/// <summary>
/// <c>reporting-events</c> from its producers (X-24). Each sample is the one the producer's own
/// <c>ReportingEventContractTests</c> serializes.
/// </summary>
public class ProducerContractTests
{
    private static readonly Dictionary<string, string> Samples = new()
    {
        ["alert-orchestrator"] = """
            {"EventId":"alert-triggered:3c9a1f0e5b7d4e2a8f6c1d0b9a8e7f6d","EventType":"AlertTriggered","TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","Region":"rj-capital","Severity":"HIGH","Channel":"none","RiskScore":0.84,"Status":"SUCCESS","Timestamp":"2026-11-01T12:00:00Z","Source":"alert-orchestrator"}
            """,
        ["channels"] = """
            {"EventId":"NotificationSent:6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f:alert:3c9a1f0e5b7d4e2a8f6c1d0b9a8e7f6d:1:sms","EventType":"NotificationSent","TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","Region":"rj-capital","Severity":"HIGH","Channel":"sms","RiskScore":0.84,"Status":"SUCCESS","Timestamp":"2026-11-01T12:00:05Z","Source":"channels"}
            """
    };

    [Theory]
    [InlineData("alert-orchestrator", "AlertTriggered", "none")]
    [InlineData("channels", "NotificationSent", "sms")]
    public void EventFromEachProducer_IsReadAndValid(string producer, string eventType, string channel)
    {
        var envelope = Assert.Single(ReportingKafkaConsumerHostedService.ParseEnvelopes(Samples[producer]));

        var result = new ReportingEventNormalizer().Normalize(envelope);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var normalized = result.NormalizedEnvelope!;
        Assert.Equal(eventType, normalized.EventType);
        Assert.Equal("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f", normalized.TenantId);
        Assert.Equal("RJ-CAPITAL", normalized.Region);
        Assert.Equal(channel, normalized.Channel);
        Assert.Equal(0.84, normalized.RiskScore);
        Assert.Equal(producer, normalized.Source);
        Assert.Equal(DateTimeKind.Utc, normalized.Timestamp.Kind);
    }
}
