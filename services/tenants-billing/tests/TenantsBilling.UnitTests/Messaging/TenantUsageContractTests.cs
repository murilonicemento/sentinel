using TenantsBilling.Infrastructure.HostedServices;

namespace TenantsBilling.UnitTests.Messaging;

/// <summary>
/// <c>tenant-usage-events</c> from its producers (X-24). Each sample is the one the producer's own
/// <c>TenantUsageContractTests</c> serializes.
/// </summary>
public class TenantUsageContractTests
{
    private static readonly Guid Tenant = Guid.Parse("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f");

    // Producer → (sample, EventsConsumed, AlertsTriggered, ChannelUsage).
    private static readonly Dictionary<string, (string Sample, int Events, int Alerts, int Channels)> Producers = new()
    {
        ["ingestion"] = (
            """{"TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","EventsConsumed":2,"AlertsTriggered":0,"ApiRequests":0,"ChannelUsage":0,"EventId":"9c2d4e6f-8a1b-8c3d-9e5f-0a1b2c3d4e5f"}""",
            2, 0, 0),
        ["alert-orchestrator"] = (
            """{"TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","EventsConsumed":0,"AlertsTriggered":1,"ApiRequests":0,"ChannelUsage":0,"EventId":"0a8c6a1e-4f2b-8d3c-9e1f-2b3c4d5e6f70"}""",
            0, 1, 0),
        ["channels"] = (
            """{"TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","EventsConsumed":0,"AlertsTriggered":0,"ApiRequests":0,"ChannelUsage":1,"EventId":"5b1e7c2d-3a4f-8e5b-8c6d-7e8f9a0b1c2d"}""",
            0, 0, 1)
    };

    [Theory]
    [InlineData("ingestion")]
    [InlineData("alert-orchestrator")]
    [InlineData("channels")]
    public void UsageFromEachProducer_IsReadWithItsTenantCountsAndEventId(string producer)
    {
        var (sample, events, alerts, channels) = Producers[producer];

        var usage = TenantUsageEventConsumerHostedService.ParseUsageEvent(sample);

        Assert.NotNull(usage);
        Assert.Equal(Tenant, usage.TenantId);
        Assert.Equal(events, usage.EventsConsumed);
        Assert.Equal(alerts, usage.AlertsTriggered);
        Assert.Equal(0, usage.ApiRequests);
        Assert.Equal(channels, usage.ChannelUsage);
        Assert.NotNull(usage.EventId);
    }
}
