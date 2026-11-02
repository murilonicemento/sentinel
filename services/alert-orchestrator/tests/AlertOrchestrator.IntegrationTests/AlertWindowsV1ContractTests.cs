using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Domain.ValueObjects;
using AlertOrchestrator.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace AlertOrchestrator.IntegrationTests;

public class AlertWindowsV1ContractTests : IClassFixture<AlertOrchestratorApiFactory>
{
    private const string BasePath = "/api/v1/alert-orchestrator/alert-windows";
    private const string ReadRole = "AlertOrchestrator.Read";

    private readonly AlertOrchestratorApiFactory _factory;

    public AlertWindowsV1ContractTests(AlertOrchestratorApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Reader(params string[] tenants) =>
        _factory.CreateClientWithToken(TestTokens.User([ReadRole], tenants));

    private async Task<List<AlertWindow>> SeedAsync(string region, string? tenantId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertWindowRepository>();
        var windows = new List<AlertWindow>();
        for (var i = 0; i < count; i++)
        {
            var window = AlertWindow.Open(region, RiskType.Flood, tenantId, 0.7, TimeSpan.FromHours(1));
            await repository.AddAsync(window);
            windows.Add(window);
        }

        return windows;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Search_ReturnsPagedEnvelopeWithStableOrderAndMetadata()
    {
        var region = $"region-{Guid.NewGuid():N}";
        var seeded = await SeedAsync(region, "tenant-a", 3);
        var expectedOrder = seeded.OrderByDescending(w => w.OpenedAt).ThenBy(w => w.Id).Select(w => w.Id).ToList();

        var first = await Reader("tenant-a").GetFromJsonAsync<JsonElement>($"{BasePath}?region={region}&page=1&pageSize=2");
        var second = await Reader("tenant-a").GetFromJsonAsync<JsonElement>($"{BasePath}?region={region}&page=2&pageSize=2");

        var page1 = first.GetProperty("data");
        Assert.True(first.GetProperty("success").GetBoolean());
        Assert.Equal(3, page1.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, page1.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
        var ids = page1.GetProperty("items").EnumerateArray()
            .Concat(second.GetProperty("data").GetProperty("items").EnumerateArray())
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
        Assert.Equal(expectedOrder, ids);
    }

    [Fact]
    public async Task Search_DefaultsToTenItemsPerPage()
    {
        var region = $"region-{Guid.NewGuid():N}";
        await SeedAsync(region, "tenant-a", 12);

        var body = await Reader("tenant-a").GetFromJsonAsync<JsonElement>($"{BasePath}?region={region}");

        Assert.Equal(10, body.GetProperty("data").GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("data").GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Search_ExcludesOtherTenantsFromItemsAndTotals()
    {
        var region = $"region-{Guid.NewGuid():N}";
        await SeedAsync(region, "tenant-a", 1);
        await SeedAsync(region, "tenant-b", 2);

        var body = await Reader("tenant-a").GetFromJsonAsync<JsonElement>($"{BasePath}?region={region}");

        Assert.Equal(1, body.GetProperty("data").GetProperty("totalItems").GetInt64());
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=51", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("status=NotAStatus", "status")]
    public async Task Search_WithInvalidParameter_ReturnsValidationProblem(string query, string field)
    {
        var response = await Reader("tenant-a").GetAsync($"{BasePath}?region=north&{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Search_WithoutFilters_ReturnsValidationProblem()
    {
        var response = await Reader("tenant-a").GetAsync(BasePath);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetById_OfAnotherTenant_ReturnsNotFoundProblem()
    {
        var window = (await SeedAsync($"region-{Guid.NewGuid():N}", "tenant-b", 1)).Single();

        var response = await Reader("tenant-a").GetAsync($"{BasePath}/{window.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ALERT_WINDOW_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetById_OfOwnTenant_ReturnsEnvelope()
    {
        var window = (await SeedAsync($"region-{Guid.NewGuid():N}", "tenant-a", 1)).Single();

        var body = await Reader("tenant-a").GetFromJsonAsync<JsonElement>($"{BasePath}/{window.Id}");

        Assert.Equal(window.Id, body.GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Search_WithoutToken_ReturnsProblemDetails401()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync($"{BasePath}?region=north");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecationAndSuccessor()
    {
        var response = await Reader("tenant-a").GetAsync("/api/alert-windows/by-region/north");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Equal("Sat, 23 Jan 2027 00:00:00 GMT", response.Headers.GetValues("Sunset").Single());
        Assert.Contains("/api/v1/alert-orchestrator/alert-windows", response.Headers.GetValues("Link").Single());
    }
}
