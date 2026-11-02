using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Write.Options;
using Xunit;

namespace Ingestion.IntegrationTests.Polling;

public class PollingOptionsTests
{
    private static PollingSubscription Valid => new()
    {
        TenantId = Guid.NewGuid(),
        DataSourceId = Guid.NewGuid(),
        Latitude = -23.5,
        Longitude = -46.6,
        StationId = "GHCND:BR000083781"
    };

    [Fact]
    public void DisabledSource_NeedsNothing()
    {
        Assert.Empty(PollingSources.Validate(PollingSources.Fire, new PollingSourceOptions()));
    }

    [Fact]
    public void EnabledSource_ReportsEveryMissingKeyByName()
    {
        var errors = PollingSources.Validate(PollingSources.Flood, new PollingSourceOptions
        {
            Enabled = true,
            Subscriptions = [Valid with { StationId = null, Latitude = 120 }]
        }).ToList();

        Assert.Contains("Polling:Flood:BaseUrl must be an absolute URL.", errors);
        Assert.Contains("Polling:Flood:ApiKey is required.", errors);
        Assert.Contains("Polling:Flood:Subscriptions:0:Latitude must be between -90 and 90.", errors);
        Assert.Contains("Polling:Flood:Subscriptions:0:StationId is required by the NOAA sources.", errors);
    }

    [Fact]
    public void Earthquake_NeedsNoKey_ButAtLeastOneSubscription()
    {
        var withoutSubscriptions = new PollingSourceOptions { Enabled = true, BaseUrl = "https://earthquake.usgs.gov" };

        Assert.Equal(["Polling:Earthquake:Subscriptions requires at least one subscription."],
            PollingSources.Validate(PollingSources.Earthquake, withoutSubscriptions));
        Assert.Empty(PollingSources.Validate(PollingSources.Earthquake,
            new PollingSourceOptions
            {
                Enabled = true,
                BaseUrl = "https://earthquake.usgs.gov",
                Subscriptions = [Valid]
            }));
    }
}
