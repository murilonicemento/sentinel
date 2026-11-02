using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;

namespace RiskCatalog.IntegrationTests.Stack;

/// <summary>
/// The deprecated <c>/api/event-types</c> and <c>/api/risk-model</c> routes end to end (kept until their sunset):
/// what is created is read back from Postgres, and a publication reaches Kafka. The routes answer 204 for missing
/// data and 400 for conflicts and unknown versions, the frozen legacy behavior (RC-02: v1 answers 404, 409 and
/// 422).
/// </summary>
[Collection(RiskCatalogStackCollection.Name)]
public class LegacyCatalogApiTests(RiskCatalogStackFactory stack)
{
    private const string Read = "RiskCatalog.Read";
    private const string Write = "RiskCatalog.Write";

    private static string NewCode() => $"EVT{Guid.NewGuid():N}"[..20];
    private static int NewVersion() => Random.Shared.Next(1_000, int.MaxValue);

    private async Task CreateEventTypeAsync(string code) =>
        Assert.Equal(HttpStatusCode.Created, (await stack.ClientFor(Write).PostAsJsonAsync("/api/event-types",
            new { code, name = "Flood", description = "River flood", isActive = true })).StatusCode);

    /// <summary>Severities are global: 400 (the legacy conflict) means another test already created this level.</summary>
    private async Task EnsureSeverityAsync(string level)
    {
        var response = await stack.ClientFor(Write).PostAsJsonAsync("/api/event-types/severity",
            new { level, description = $"{level} severity" });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.BadRequest });
    }

    [Fact]
    public async Task EventType_CreatedIsReadBackByCodeAndInTheList()
    {
        var code = NewCode();

        await CreateEventTypeAsync(code);

        var client = stack.ClientFor(Read);
        var byCode = await client.GetFromJsonAsync<JsonElement>(
            $"/api/event-types/event-type-by-code?eventTypeCode={code}");
        Assert.Equal("Flood", byCode.GetProperty("data").GetProperty("name").GetString());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/event-types?isActive=true");
        Assert.Contains(list.GetProperty("data").EnumerateArray(), item => item.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task EventType_Duplicated_Returns400OnTheLegacyRoute()
    {
        var code = NewCode();
        await CreateEventTypeAsync(code);

        var response = await stack.ClientFor(Write).PostAsJsonAsync("/api/event-types",
            new { code, name = "Flood", description = "River flood", isActive = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1L, await stack.CountAsync("SELECT count(*) FROM event_type WHERE code = @p", code));
    }

    [Fact]
    public async Task EventType_WithMissingFields_Returns400AndWritesNothing()
    {
        var code = NewCode();

        var response = await stack.ClientFor(Write).PostAsJsonAsync("/api/event-types", new { code, name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await stack.CountAsync("SELECT count(*) FROM event_type WHERE code = @p", code));
    }

    [Fact]
    public async Task UnknownEventType_Returns204()
    {
        var response = await stack.ClientFor(Read)
            .GetAsync($"/api/event-types/event-type-by-code?eventTypeCode={NewCode()}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SeverityCriterion_CreatedIsReadBackForItsVersion()
    {
        var code = NewCode();
        var version = NewVersion();
        await CreateEventTypeAsync(code);
        await EnsureSeverityAsync("High");

        var created = await stack.ClientFor(Write).PostAsJsonAsync("/api/event-types/severity-criterion", new
        {
            eventTypeCode = code,
            severityLevel = "High",
            minValue = 50.0,
            maxValue = 80.0,
            unit = "mm",
            version
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var criteria = await stack.ClientFor(Read).GetFromJsonAsync<JsonElement>(
            $"/api/event-types/severity-criterion?eventTypeCode={code}&version={version}");
        var criterion = Assert.Single(criteria.GetProperty("data").EnumerateArray());
        Assert.Equal(50.0, criterion.GetProperty("minValue").GetDouble());
    }

    [Fact]
    public async Task RiskMatrix_CreatedIsReadBack()
    {
        var code = NewCode();
        var version = NewVersion();
        await CreateEventTypeAsync(code);
        await EnsureSeverityAsync("Extreme");

        var created = await stack.ClientFor(Write).PostAsJsonAsync("/api/risk-model/risk-matrix",
            new { eventTypeCode = code, severityLevel = "Extreme", riskLevel = "Critical", version });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var matrix = await stack.ClientFor(Read).GetFromJsonAsync<JsonElement>(
            $"/api/risk-model/risk-matrix?eventTypeCode={code}&severityLevel=Extreme&version={version}");
        Assert.Equal("Critical", matrix.GetProperty("data").GetProperty("riskLevel").GetString());
    }

    [Fact]
    public async Task IdfCurve_CreatedIsReadBack()
    {
        var code = NewCode();
        await CreateEventTypeAsync(code);

        var created = await stack.ClientFor(Write).PostAsJsonAsync("/api/risk-model/idf-curves", new
        {
            eventTypeCode = code,
            durationMinutes = 60,
            intensity = 42.5,
            returnPeriodYears = 10,
            version = NewVersion()
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var curves = await stack.ClientFor(Read).GetFromJsonAsync<JsonElement>(
            $"/api/risk-model/idf-curves?eventTypeCode={code}&returnPeriodYears=10");
        Assert.Equal(42.5, curves.GetProperty("data").GetProperty("intensity").GetDouble());
    }

    [Fact]
    public async Task RegionalParameters_CreatedArePersisted()
    {
        var description = $"Região {Guid.NewGuid():N}";

        var created = await stack.ClientFor(Write).PostAsJsonAsync("/api/risk-model/regional-risk-parameters", new
        {
            adjustmentFactor = 1.5,
            description,
            centerLatitude = -23.55,
            centerLongitude = -46.63,
            coverageRadiusKm = 50.0
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1L, await stack.CountAsync(
            "SELECT count(*) FROM regional_parameter WHERE description = @p", description));
    }

    [Fact]
    public async Task Publish_MarksTheVersionAndAnnouncesItOnKafka()
    {
        var code = NewCode();
        var version = NewVersion();
        await CreateEventTypeAsync(code);
        await EnsureSeverityAsync("Moderate");
        (await stack.ClientFor(Write).PostAsJsonAsync("/api/risk-model/risk-matrix",
            new { eventTypeCode = code, severityLevel = "Moderate", riskLevel = "Medium", version }))
            .EnsureSuccessStatusCode();
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = stack.KafkaBootstrapServers,
            GroupId = $"risk-catalog-tests-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        consumer.Subscribe("risk-catalog-published");

        var response = await stack.ClientFor(Write).PostAsJsonAsync("/api/risk-model/catalog/publish",
            new { version, notes = "Integration test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement? announced = null;
        var deadline = Stopwatch.StartNew();
        while (announced is null && deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result?.Message is null) continue;
            var payload = JsonDocument.Parse(result.Message.Value).RootElement;
            if (payload.GetProperty("version").GetInt32() == version) announced = payload;
        }

        Assert.NotNull(announced);
        Assert.Equal(RiskCatalogStackFactory.PublisherTenant, announced.Value.GetProperty("publishedBy").GetGuid());
    }

    [Fact]
    public async Task Publish_OfAnUnknownVersion_Returns400OnTheLegacyRoute()
    {
        var response = await stack.ClientFor(Write).PostAsJsonAsync("/api/risk-model/catalog/publish",
            new { version = NewVersion(), notes = "nothing to publish" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/event-types")]
    [InlineData("/api/risk-model/catalog/publish")]
    public async Task Writes_WithoutToken_Return401(string path)
    {
        var response = await stack.CreateClient().PostAsJsonAsync(path, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Writes_WithAReadOnlyToken_Return403AndWriteNothing()
    {
        var code = NewCode();

        var response = await stack.ClientFor(Read).PostAsJsonAsync("/api/event-types",
            new { code, name = "Flood", description = "River flood", isActive = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await stack.CountAsync("SELECT count(*) FROM event_type WHERE code = @p", code));
    }
}
