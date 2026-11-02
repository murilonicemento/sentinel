using AlertOrchestrator.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlertOrchestrator.UnitTests.Configuration;

/// <summary>
/// Every signal comes from risk-updated, tagged as a sensor reading, so a default rule that asked for a satellite or
/// for distinct sources could never trigger (X-24).
/// </summary>
public class DefaultTriggerRulesTests
{
    [Theory]
    [InlineData("flood")]
    [InlineData("wildfire")]
    [InlineData("earthquake")]
    [InlineData("landslide")]
    [InlineData("storm")]
    [InlineData("heatwave")]
    [InlineData("rainfall")]
    public async Task DefaultRule_CanBeMetByRiskSignalsAlone(string riskType)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var port = new InMemoryConfigurationPort(cache, NullLogger<InMemoryConfigurationPort>.Instance);

        var rule = await port.GetConfigurationAsync(riskType, "tenant-1");

        Assert.Equal(1, rule.RequiredDistinctSources);
        Assert.False(rule.RequireSatellite);
    }
}
