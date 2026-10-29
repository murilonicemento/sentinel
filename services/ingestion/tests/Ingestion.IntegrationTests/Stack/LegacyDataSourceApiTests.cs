using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Ingestion.IntegrationTests.Stack;

/// <summary>The deprecated <c>/api/ingestion/data-source</c> route, end to end (kept until its sunset).</summary>
[Collection(IngestionStackCollection.Name)]
public class LegacyDataSourceApiTests(IngestionStackFactory stack)
{
    private const string Route = "/api/ingestion/data-source";

    private static object DataSource(Guid tenantId, string name, string type = "Sensor") => new
    {
        tenantId,
        name,
        endpoint = "http://sensors.example.com/rain",
        dataSourceType = type,
        measurementType = "Rainfall",
        collectionFrequency = "Hourly",
        eventPermissions = new[] { new { eventDomain = "Climatic", eventType = "Rainfall" } }
    };

    [Fact]
    public async Task Register_PersistsTheDataSourceWithItsPermissions()
    {
        var name = $"rain-{Guid.NewGuid():N}";

        var response = await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA)
            .PostAsJsonAsync(Route, DataSource(IngestionStackFactory.TenantA, name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("data").GetProperty("dataSourceId").GetGuid();
        Assert.Equal(name, await stack.ScalarAsync<string>("SELECT name FROM data_source WHERE id = @p", id));
        Assert.Equal(1L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM event_type_permission WHERE data_source_id = @p", id));
    }

    [Theory]
    [InlineData("", "Sensor")]
    [InlineData("rain", "InvalidType")]
    public async Task Register_WithInvalidFields_Returns400AndWritesNothing(string name, string type)
    {
        var before = await stack.ScalarAsync<long>("SELECT count(*) FROM data_source");

        var response = await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA)
            .PostAsJsonAsync(Route, DataSource(IngestionStackFactory.TenantA, name, type));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await stack.ScalarAsync<long>("SELECT count(*) FROM data_source"));
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        var response = await stack.CreateClient()
            .PostAsJsonAsync(Route, DataSource(IngestionStackFactory.TenantA, "rain"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_ForATenantOutsideTheToken_Returns403AndWritesNothing()
    {
        var name = $"rain-{Guid.NewGuid():N}";

        var response = await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA)
            .PostAsJsonAsync(Route, DataSource(IngestionStackFactory.TenantB, name));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await stack.ScalarAsync<long>("SELECT count(*) FROM data_source WHERE name = @p", name));
    }

    /// <summary>Regression: one Postgres connection was shared by every request and failed under concurrency.</summary>
    [Fact]
    public async Task ConcurrentRegistrations_AllSucceed()
    {
        var client = stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA);

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            client.PostAsJsonAsync(Route, DataSource(IngestionStackFactory.TenantA, $"rain-{Guid.NewGuid():N}"))));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
    }

    [Fact]
    public async Task LegacyLastDetectedEvents_AnswersWithAReadToken()
    {
        var response = await stack.ClientFor("Ingestion.Read", IngestionStackFactory.TenantA)
            .GetAsync("/api/ingestion/last-detected-events?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Frozen legacy behavior: the route still reads the old <c>events</c> collection (ING-10, fixed only in v1), so
    /// it finds no statistics and answers 204. Consumers should move to <c>/api/v1/ingestion/detected-events/statistics</c>.
    /// </summary>
    [Fact]
    public async Task LegacyCollectionStatistics_AnswersNoContentFromTheOldCollection()
    {
        var response = await stack.ClientFor("Ingestion.Read", IngestionStackFactory.TenantA)
            .GetAsync("/api/ingestion/collection-statistics");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LegacyQueries_WithoutToken_Return401()
    {
        var response = await stack.CreateClient().GetAsync("/api/ingestion/last-detected-events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
