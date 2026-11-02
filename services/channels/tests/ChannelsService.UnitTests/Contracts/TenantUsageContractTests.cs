using System.Text.Json;
using Xunit;
using ChannelsService.Application.DTOs;

namespace ChannelsService.UnitTests.Contracts;

/// <summary>
/// <c>tenant-usage-events</c> as tenants-billing reads it (X-24). The same sample is parsed by tenants-billing's
/// <c>TenantUsageContractTests</c>.
/// </summary>
public class TenantUsageContractTests
{
    private const string Sample = """
        {"TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","EventsConsumed":0,"AlertsTriggered":0,"ApiRequests":0,"ChannelUsage":1,"EventId":"5b1e7c2d-3a4f-8e5b-8c6d-7e8f9a0b1c2d"}
        """;

    [Fact]
    public void DeliveredChannel_SerializesToTheContract()
    {
        var usage = new TenantUsageEventDTO(Guid.Parse("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f"), 0, 0, 0, 1,
            Guid.Parse("5b1e7c2d-3a4f-8e5b-8c6d-7e8f9a0b1c2d"));

        Assert.Equal(Sample.Trim(), JsonSerializer.Serialize(usage));
    }
}
