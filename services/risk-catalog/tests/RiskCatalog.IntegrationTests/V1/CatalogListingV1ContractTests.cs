using System.Net;
using System.Text.Json;
using Moq;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.IntegrationTests.Support;

namespace RiskCatalog.IntegrationTests.V1;

/// <summary>Field names risk-evaluation's RiskCatalogHttpClient reads to build its catalog snapshot.</summary>
public class CatalogListingV1ContractTests : IClassFixture<RiskCatalogV1ApiFactory>
{
    private const string BasePath = "/api/v1/risk-catalog";

    private readonly RiskCatalogV1ApiFactory _factory;

    public CatalogListingV1ContractTests(RiskCatalogV1ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ServiceClient() => _factory.CreateClientWithToken(TestTokens.User(["RiskCatalog.Read"]));

    [Fact]
    public async Task RegionalParameters_ExposeFactorCenterRadiusAndBounds()
    {
        _factory.RegionalParameters.Setup(r => r.GetPageAsync(1, 50, It.IsAny<CancellationToken>())).ReturnsAsync(
            (new List<RegionalParameter>
            {
                new(Guid.NewGuid(), 1.4, "coast", -23.5, -46.6,
                    "{\"Coordinates\":[{\"Latitude\":-23,\"Longitude\":-47}]}", 20)
            }, 1L));

        var body = await ServiceClient().GetStringAsync($"{BasePath}/regional-risk-parameters?page=1&pageSize=50");

        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("totalPages").GetInt32());
        var region = data.GetProperty("items")[0];
        Assert.Equal(1.4, region.GetProperty("adjustmentFactor").GetDouble());
        Assert.Equal(20, region.GetProperty("coverageRadiusKm").GetDouble());
        Assert.Equal(-23, region.GetProperty("regionBounds").GetProperty("coordinates")[0].GetProperty("latitude").GetDouble());
        Assert.True(region.TryGetProperty("regionId", out _));
    }

    [Fact]
    public async Task MatrixEntries_AndSeverityCriteria_AreFilteredByVersion()
    {
        var eventType = new EventType(Guid.NewGuid(), "Flood", "Flood", "d", true);
        _factory.RiskMatrices.Setup(r => r.GetPageAsync(3, 1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(
            (new List<RiskMatrix>
            {
                new(Guid.NewGuid(), eventType.Id, SeverityLevelEnum.High, RiskLevelEnum.Critical, 3)
                    { EventType = eventType }
            }, 1L));
        _factory.EventTypeRepository.Setup(r => r.GetSeverityCriteriaPageAsync(3, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SeverityCriterion>
            {
                new(Guid.NewGuid(), eventType.Id, Guid.NewGuid(), 5, 10, "m", 3)
                {
                    EventType = eventType, Severity = new Severity(Guid.NewGuid(), SeverityLevelEnum.High, "high")
                }
            }, 1L));

        var matrix = JsonDocument.Parse(await ServiceClient().GetStringAsync($"{BasePath}/risk-matrix-entries?version=3"))
            .RootElement.GetProperty("data").GetProperty("items")[0];
        var criterion = JsonDocument.Parse(await ServiceClient().GetStringAsync($"{BasePath}/severity-criteria?version=3"))
            .RootElement.GetProperty("data").GetProperty("items")[0];

        Assert.Equal(("Flood", "High", "Critical"), (matrix.GetProperty("eventTypeCode").GetString(),
            matrix.GetProperty("severityLevel").GetString(), matrix.GetProperty("riskLevel").GetString()));
        Assert.Equal((5d, 10d), (criterion.GetProperty("minValue").GetDouble(), criterion.GetProperty("maxValue").GetDouble()));
    }

    [Fact]
    public async Task Listings_RequireTheReadRole()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.User(["Other.Read"]))
            .GetAsync($"{BasePath}/risk-matrix-entries");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
