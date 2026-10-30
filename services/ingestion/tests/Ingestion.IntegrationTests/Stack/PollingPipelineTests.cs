using System.Diagnostics;
using Xunit;

namespace Ingestion.IntegrationTests.Stack;

/// <summary>
/// Polling end to end: the real USGS client, DI and scheduler against a stubbed provider, into the same stores and
/// outbox as a submitted collection.
/// </summary>
[Collection(IngestionStackCollection.Name)]
public class PollingPipelineTests(IngestionStackFactory stack)
{
    [Fact]
    public async Task PolledEarthquakes_AreStoredWithTheirOutboxRows_DespiteTheHourlyFrequency()
    {
        _ = stack.Server; // starts the host, whose first polling pass runs immediately

        var deadline = Stopwatch.StartNew();
        long stored = 0;
        while (stored < IngestionStackFactory.PolledEarthquakes.Length && deadline.Elapsed < TimeSpan.FromSeconds(60))
        {
            stored = await stack.ScalarAsync<long>("SELECT count(*) FROM data_collection WHERE data_source_id = @p",
                IngestionStackFactory.EarthquakeDataSource);
            if (stored < IngestionStackFactory.PolledEarthquakes.Length) await Task.Delay(500);
        }

        Assert.Equal(IngestionStackFactory.PolledEarthquakes.Length, stored);
        Assert.Equal(2L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM outbox o JOIN data_collection c ON c.id = o.aggregate_id WHERE c.data_source_id = @p",
            IngestionStackFactory.EarthquakeDataSource));
    }
}
