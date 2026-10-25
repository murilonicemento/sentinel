using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Errors;
using Ingestion.Application.Queries;
using Moq;
using Xunit;

namespace Ingestion.IntegrationTests.V1;

public class IngestionV1ContractTests : IClassFixture<IngestionV1ApiFactory>
{
    private const string BasePath = "/api/v1/ingestion";

    private readonly IngestionV1ApiFactory _factory;

    public IngestionV1ContractTests(IngestionV1ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client(string role, params Guid[] tenants) =>
        _factory.CreateClientWithToken(TestTokens.User([role], tenants));

    private static object Collection(Guid tenantId) => new
    {
        datasourceId = Guid.NewGuid(),
        tenantId,
        collectedAt = DateTime.UtcNow.AddMinutes(-1),
        payload = "{\"value\":1}",
        sampleSensors = new[]
        {
            new { sensorValue = 10.0, unit = "mm", latitude = -23.5, longitude = -46.6, recordedAt = DateTime.UtcNow.AddMinutes(-2) }
        }
    };

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task GetDetectedEvents_ScopesQueryToCallerTenants()
    {
        var tenant = Guid.NewGuid();
        GetDetectedEventsPageQuery? sent = null;
        _factory.Mediator
            .Setup(m => m.Send(It.IsAny<GetDetectedEventsPageQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((query, _) => sent = (GetDetectedEventsPageQuery)query)
            .ReturnsAsync(PagedResultDTO<DetectedEventDTO>.Create(
                [new DetectedEventDTO(Guid.NewGuid(), Guid.NewGuid(), tenant, "Climatic", "Rainfall", 10, -23.5,
                    -46.6, DateTime.UtcNow)], 2, 5, 6));

        var body = await Client("Ingestion.Read", tenant)
            .GetFromJsonAsync<JsonElement>($"{BasePath}/detected-events?page=2&pageSize=5");

        Assert.NotNull(sent);
        Assert.False(sent.Scope.AllTenants);
        Assert.Equal(tenant, Assert.Single(sent.Scope.TenantIds));
        Assert.Equal((2, 5), (sent.Page, sent.PageSize));
        var data = body.GetProperty("data");
        Assert.Equal(6, data.GetProperty("totalItems").GetInt64());
        Assert.Equal("Rainfall", data.GetProperty("items")[0].GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task GetDetectedEvents_WithInvalidPage_ReturnsFieldError()
    {
        var response = await Client("Ingestion.Read", Guid.NewGuid()).GetAsync($"{BasePath}/detected-events?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("page", out _));
    }

    [Fact]
    public async Task GetStatistics_WithInitialAfterEnd_ReturnsFieldError()
    {
        var response = await Client("Ingestion.Read", Guid.NewGuid()).GetAsync(
            $"{BasePath}/detected-events/statistics?initialDate=2026-10-02T00:00:00Z&endDate=2026-10-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("initialDate", out _));
    }

    [Fact]
    public async Task GetStatistics_ReturnsEnvelope()
    {
        _factory.Mediator
            .Setup(m => m.Send(It.IsAny<GetDetectedEventStatisticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DetectedEventStatisticsDTO
            {
                TotalEvents = 2,
                TotalByType = new Dictionary<string, int> { ["Flood"] = 2 },
                MaxIntensity = 9
            });

        var body = await Client("Ingestion.Read", Guid.NewGuid())
            .GetFromJsonAsync<JsonElement>($"{BasePath}/detected-events/statistics");

        Assert.Equal(2, body.GetProperty("data").GetProperty("totalByType").GetProperty("Flood").GetInt32());
    }

    [Fact]
    public async Task SensorCollection_ReturnsAcceptedWithDataSource()
    {
        var tenant = Guid.NewGuid();
        var dataSource = Guid.NewGuid();
        _factory.Mediator
            .Setup(m => m.Send(It.Is<RegisterSensorCollectionCommand>(c => c.TenantId == tenant),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataSource);

        var response = await Client("Ingestion.Write", tenant).PostAsJsonAsync($"{BasePath}/sensor-collections",
            Collection(tenant));

        Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        Assert.Equal(dataSource, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("dataSourceId").GetGuid());
    }

    [Fact]
    public async Task ClimaticCollection_ForAnotherTenant_ReturnsTenantAccessDenied()
    {
        var response = await Client("Ingestion.Write", Guid.NewGuid()).PostAsJsonAsync(
            $"{BasePath}/climatic-collections", Collection(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_ACCESS_DENIED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task DisasterCollection_ForUnknownDataSource_Returns422()
    {
        var tenant = Guid.NewGuid();
        _factory.Mediator
            .Setup(m => m.Send(It.Is<RegisterDisasterEventCommand>(c => c.TenantId == tenant),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IngestionReferenceNotFoundException(IngestionErrorCodes.DataSourceNotFound,
                "DataSource or tenant not found."));

        var response = await Client("Ingestion.Write", tenant).PostAsJsonAsync($"{BasePath}/disaster-collections",
            Collection(tenant));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("DATA_SOURCE_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task SensorCollection_WithoutPayload_ReturnsValidationProblem()
    {
        var tenant = Guid.NewGuid();

        var response = await Client("Ingestion.Write", tenant).PostAsJsonAsync($"{BasePath}/sensor-collections",
            new { datasourceId = Guid.NewGuid(), tenantId = tenant, collectedAt = DateTime.UtcNow.AddMinutes(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task RegisterDataSource_WhenDuplicated_Returns409()
    {
        var tenant = Guid.NewGuid();
        _factory.Mediator
            .Setup(m => m.Send(It.Is<RegisterDataSourceCommand>(c => c.TenantId == tenant),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IngestionRuleViolationException(IngestionErrorKind.Conflict,
                IngestionErrorCodes.DataSourceAlreadyExists, "DataSource 'Rain' already exists for this tenant."));

        var response = await Client("Ingestion.Write", tenant).PostAsJsonAsync($"{BasePath}/data-sources", new
        {
            tenantId = tenant,
            name = "Rain",
            dataSourceType = "Api",
            measurementType = "Rainfall",
            endpoint = "https://sensors.example.com",
            collectionFrequency = "Hourly",
            eventPermissions = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DATA_SOURCE_ALREADY_EXISTS", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecation()
    {
        var response = await Client("Ingestion.Write", Guid.NewGuid()).PostAsJsonAsync(
            "/api/ingestion/climatic", Collection(Guid.NewGuid()));

        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Contains("/api/v1/ingestion", response.Headers.GetValues("Link").Single());
    }
}
