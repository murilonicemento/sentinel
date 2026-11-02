using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Errors;
using RiskCatalog.IntegrationTests.Support;

namespace RiskCatalog.IntegrationTests.V1;

public class RiskCatalogV1ContractTests : IClassFixture<RiskCatalogV1ApiFactory>
{
    private const string BasePath = "/api/v1/risk-catalog";

    private readonly RiskCatalogV1ApiFactory _factory;

    public RiskCatalogV1ContractTests(RiskCatalogV1ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Reader() => _factory.CreateClientWithToken(TestTokens.User(["RiskCatalog.Read"]));

    private HttpClient Writer(params Guid[] tenants) =>
        _factory.CreateClientWithToken(TestTokens.User(["RiskCatalog.Write"], tenants));

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task ListEventTypes_ReturnsPageOrderedByCode()
    {
        _factory.EventTypes
            .Setup(s => s.GetEventTypesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EventTypeDTO { Id = Guid.NewGuid(), Code = "LANDSLIDE", Name = "Landslide" },
                new EventTypeDTO { Id = Guid.NewGuid(), Code = "FIRE", Name = "Fire" },
                new EventTypeDTO { Id = Guid.NewGuid(), Code = "FLOOD", Name = "Flood" }
            ]);

        var body = await Reader().GetFromJsonAsync<JsonElement>($"{BasePath}/event-types?page=1&pageSize=2");

        var data = body.GetProperty("data");
        Assert.Equal(3, data.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, data.GetProperty("totalPages").GetInt32());
        Assert.Equal(["FIRE", "FLOOD"], data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task ListEventTypes_WithPageSizeAboveLimit_ReturnsFieldError()
    {
        var response = await Reader().GetAsync($"{BasePath}/event-types?pageSize=51");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task GetEventType_WhenMissing_Returns404Problem()
    {
        _factory.EventTypes
            .Setup(s => s.GetEventTypeByCodeAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventTypeDTO?)null);

        var response = await Reader().GetAsync($"{BasePath}/event-types/UNKNOWN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("EVENT_TYPE_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetEventType_WhenFound_ReturnsSingleEnvelope()
    {
        _factory.EventTypes
            .Setup(s => s.GetEventTypeByCodeAsync("FLOOD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventTypeDTO { Id = Guid.NewGuid(), Code = "FLOOD", Name = "Flood" });

        var body = await Reader().GetFromJsonAsync<JsonElement>($"{BasePath}/event-types/FLOOD");

        Assert.Equal(200, body.GetProperty("statusCode").GetInt32());
        Assert.Equal("FLOOD", body.GetProperty("data").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateEventType_ReturnsCreatedWithLocation()
    {
        _factory.EventTypes
            .Setup(s => s.CreateEventTypeAsync(It.Is<CreateEventTypeDTO>(d => d.Code == "HAIL"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await Writer().PostAsJsonAsync($"{BasePath}/event-types",
            new { code = "HAIL", name = "Hail", description = "Hail storm", isActive = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{BasePath}/event-types/HAIL", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CreateEventType_WhenDuplicated_Returns409WithStableCode()
    {
        _factory.EventTypes
            .Setup(s => s.CreateEventTypeAsync(It.Is<CreateEventTypeDTO>(d => d.Code == "DUP"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RiskCatalogException(RiskCatalogErrorKind.Conflict,
                RiskCatalogErrorCodes.EventTypeAlreadyExists, "Event type with the same code already exists."));

        var response = await Writer().PostAsJsonAsync($"{BasePath}/event-types",
            new { code = "DUP", name = "Dup", description = "Duplicated", isActive = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EVENT_TYPE_ALREADY_EXISTS", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateEventType_WithoutName_ReturnsValidationProblem()
    {
        var response = await Writer().PostAsJsonAsync($"{BasePath}/event-types", new { code = "X", description = "d" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateEventType_WithReadRole_IsForbidden()
    {
        var response = await Reader().PostAsJsonAsync($"{BasePath}/event-types",
            new { code = "HAIL", name = "Hail", description = "Hail storm", isActive = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WhenMissing_Returns404InsteadOfSuccess()
    {
        var id = Guid.NewGuid();
        _factory.EventTypes
            .Setup(s => s.UpdateEventTypeStatusAsync(id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await Writer().PatchAsJsonAsync($"{BasePath}/event-types/{id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WhenUpdated_ReturnsNoContentWithoutBody()
    {
        var id = Guid.NewGuid();
        _factory.EventTypes
            .Setup(s => s.UpdateEventTypeStatusAsync(id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await Writer().PatchAsJsonAsync($"{BasePath}/event-types/{id}/status", new { isActive = true });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetRiskMatrix_WithoutRequiredQuery_ReturnsFieldErrors()
    {
        var response = await Reader().GetAsync($"{BasePath}/risk-matrices");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("eventTypeCode", out _));
        Assert.True(errors.TryGetProperty("severityLevel", out _));
    }

    [Fact]
    public async Task GetRiskMatrix_WhenMissing_Returns404InsteadOf204()
    {
        _factory.RiskModels
            .Setup(s => s.GetRiskMatrixForEventTypeAsync("FLOOD", "High", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrixDTO?)null);

        var response = await Reader().GetAsync($"{BasePath}/risk-matrices?eventTypeCode=FLOOD&severityLevel=High");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("RISK_MATRIX_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateRiskMatrix_ForUnknownEventType_Returns422()
    {
        _factory.RiskModels
            .Setup(s => s.CreateRiskMatrixAsync(It.Is<RiskMatrixDTO>(d => d.EventTypeCode == "NOPE"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RiskCatalogException(RiskCatalogErrorKind.Unprocessable,
                RiskCatalogErrorCodes.EventTypeNotFound, "Event type does not exist."));

        var response = await Writer().PostAsJsonAsync($"{BasePath}/risk-matrices",
            new { eventTypeCode = "NOPE", severityLevel = "High", riskLevel = "Critical", version = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("EVENT_TYPE_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Publish_WithoutTenantClaim_ReturnsTenantContextRequired()
    {
        var response = await Writer().PostAsJsonAsync($"{BasePath}/catalog-publications", new { version = 1, notes = "n" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_CONTEXT_REQUIRED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Publish_WithTenantClaim_ReturnsAccepted()
    {
        var tenant = Guid.NewGuid();
        _factory.RiskModels
            .Setup(s => s.PublishCatalogVersionAsync(It.Is<CatalogPublishDTO>(d => d.Version == 7), tenant,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await Writer(tenant).PostAsJsonAsync($"{BasePath}/catalog-publications", new { version = 7, notes = "n" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(7, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task UnexpectedFailure_Returns500ProblemWithoutInternals()
    {
        _factory.EventTypes
            .Setup(s => s.GetEventTypeByCodeAsync("BOOM", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Npgsql connection to 10.1.2.3 refused"));

        var response = await Reader().GetAsync($"{BasePath}/event-types/BOOM");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("10.1.2.3", (await ProblemAsync(response)).GetRawText());
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecation()
    {
        _factory.EventTypes
            .Setup(s => s.GetEventTypesAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await Reader().GetAsync("/api/event-types");

        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Contains("/api/v1/risk-catalog/event-types", response.Headers.GetValues("Link").Single());
    }
}
