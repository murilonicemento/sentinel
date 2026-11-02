using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Domain.ValueObjects;
using AlertOrchestrator.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace AlertOrchestrator.IntegrationTests;

public class AlertWindowsTenantIsolationTests : IClassFixture<AlertOrchestratorApiFactory>
{
    private const string ReadRole = "AlertOrchestrator.Read";

    private readonly AlertOrchestratorApiFactory _factory;

    public AlertWindowsTenantIsolationTests(AlertOrchestratorApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(AlertWindow TenantA, AlertWindow TenantB, AlertWindow Platform)> SeedRegionAsync(string region)
    {
        var tenantA = AlertWindow.Open(region, RiskType.Flood, "tenant-a", 0.7, TimeSpan.FromHours(1));
        var tenantB = AlertWindow.Open(region, RiskType.Flood, "tenant-b", 0.7, TimeSpan.FromHours(1));
        var platform = AlertWindow.Open(region, RiskType.Flood, null, 0.7, TimeSpan.FromHours(1));

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertWindowRepository>();
        await repository.AddAsync(tenantA);
        await repository.AddAsync(tenantB);
        await repository.AddAsync(platform);

        return (tenantA, tenantB, platform);
    }

    private static async Task<List<Guid>> WindowIdsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.ValueKind == JsonValueKind.Array ? body : body.GetProperty("data");
        return items.EnumerateArray().Select(w => w.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task GetByRegion_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync("/api/alert-windows/by-region/north");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetByRegion_ReturnsOnlyWindowsOfTheCallersTenants()
    {
        var region = $"region-{Guid.NewGuid():N}";
        var (tenantA, _, _) = await SeedRegionAsync(region);

        var response = await _factory.CreateClientWithToken(TestTokens.User([ReadRole], ["tenant-a"]))
            .GetAsync($"/api/alert-windows/by-region/{region}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([tenantA.Id], await WindowIdsAsync(response));
    }

    [Fact]
    public async Task GetByRegion_WithTrustedServiceToken_ReturnsAllWindows()
    {
        var region = $"region-{Guid.NewGuid():N}";
        var (tenantA, tenantB, platform) = await SeedRegionAsync(region);

        var response = await _factory.CreateClientWithToken(TestTokens.Service([ReadRole]))
            .GetAsync($"/api/alert-windows/by-region/{region}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new HashSet<Guid> { tenantA.Id, tenantB.Id, platform.Id }, (await WindowIdsAsync(response)).ToHashSet());
    }

    [Fact]
    public async Task GetById_OfAnotherTenant_IsReportedAsMissing()
    {
        var (_, tenantB, _) = await SeedRegionAsync($"region-{Guid.NewGuid():N}");

        var response = await _factory.CreateClientWithToken(TestTokens.User([ReadRole], ["tenant-a"]))
            .GetAsync($"/api/alert-windows/{tenantB.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OfOwnTenant_Returns200()
    {
        var (tenantA, _, _) = await SeedRegionAsync($"region-{Guid.NewGuid():N}");

        var response = await _factory.CreateClientWithToken(TestTokens.User([ReadRole], ["tenant-a"]))
            .GetAsync($"/api/alert-windows/{tenantA.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetByRegion_WithoutReadRole_Returns403()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.User(["Other.Role"], ["tenant-a"]))
            .GetAsync("/api/alert-windows/by-region/north");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
