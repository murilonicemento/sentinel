using System.Text.Json;
using AlertOrchestrator.Application.Events;

namespace AlertOrchestrator.UnitTests.Contracts;

/// <summary>
/// <c>tenant-usage-events</c> as tenants-billing reads it (X-24). The same sample is parsed by tenants-billing's
/// <c>TenantUsageContractTests</c>.
/// </summary>
public class TenantUsageContractTests
{
    private const string Sample = """
        {"TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","EventsConsumed":0,"AlertsTriggered":1,"ApiRequests":0,"ChannelUsage":0,"EventId":"0a8c6a1e-4f2b-8d3c-9e1f-2b3c4d5e6f70"}
        """;

    [Fact]
    public void TriggeredAlert_SerializesToTheContract()
    {
        var usage = new TenantUsageEvent(Guid.Parse("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f"), 0, 1, 0, 0,
            Guid.Parse("0a8c6a1e-4f2b-8d3c-9e1f-2b3c4d5e6f70"));

        Assert.Equal(Sample.Trim(), JsonSerializer.Serialize(usage));
    }

    [Fact]
    public void TriggeredAlert_HasTheSameIdForTheSameWindow()
    {
        var tenant = Guid.NewGuid().ToString();
        var window = Guid.NewGuid();

        var first = TenantUsageEvent.ForTriggeredAlert(tenant, window)!;

        Assert.Equal(first, TenantUsageEvent.ForTriggeredAlert(tenant, window));
        Assert.NotEqual(first.EventId, TenantUsageEvent.ForTriggeredAlert(tenant, Guid.NewGuid())!.EventId);
        Assert.Equal('8', first.EventId.ToString()[14]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tenant-1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TriggeredAlert_WithoutATenantId_HasNoUsage(string? tenantId)
    {
        Assert.Null(TenantUsageEvent.ForTriggeredAlert(tenantId, Guid.NewGuid()));
    }
}
