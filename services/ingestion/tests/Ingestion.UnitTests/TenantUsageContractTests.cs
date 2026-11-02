using System.Text.Json;
using Ingestion.Application.Events;
using Xunit;

namespace Ingestion.UnitTests;

/// <summary>
/// <c>tenant-usage-events</c> as tenants-billing reads it (X-24). The same sample is parsed by tenants-billing's
/// <c>TenantUsageContractTests</c>.
/// </summary>
public class TenantUsageContractTests
{
    private const string Sample = """
        {"TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","EventsConsumed":2,"AlertsTriggered":0,"ApiRequests":0,"ChannelUsage":0,"EventId":"9c2d4e6f-8a1b-8c3d-9e5f-0a1b2c3d4e5f"}
        """;

    [Fact]
    public void CollectionUsage_SerializesToTheContract()
    {
        var usage = new TenantUsageEvent(Guid.Parse("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f"), 2, 0, 0, 0,
            Guid.Parse("9c2d4e6f-8a1b-8c3d-9e5f-0a1b2c3d4e5f"));

        Assert.Equal(Sample.Trim(), JsonSerializer.Serialize(usage));
    }
}
