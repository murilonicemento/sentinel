using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using TenantsBilling.IntegrationTests.Support;

namespace TenantsBilling.IntegrationTests;

public class TenantsApiAuthorizationTests : IClassFixture<TenantsBillingApiFactory>
{
    private const string AdminRole = "TenantsBilling.Admin";
    private const string ReadRole = "TenantsBilling.Read";

    private readonly TenantsBillingApiFactory _factory;

    public TenantsApiAuthorizationTests(TenantsBillingApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWith(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var admin = ClientWith(TestTokens.User([AdminRole]));
        var planResponse = await admin.PostAsJsonAsync("/api/v1/plans",
            new { name = $"plan-{Guid.NewGuid():N}", maxEventsPerMonth = 100, maxAlertsPerMonth = 10, maxApiRequestsPerMonth = 100, maxChannelsPerMonth = 5 });
        planResponse.EnsureSuccessStatusCode();
        var planId = (await planResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var tenantResponse = await admin.PostAsJsonAsync("/api/v1/tenants",
            new { name = $"tenant-{Guid.NewGuid():N}", planId });
        tenantResponse.EnsureSuccessStatusCode();
        return (await tenantResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await ClientWith(null).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithoutToken_Returns401()
    {
        var response = await ClientWith(null).GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithTokenFromUnknownIssuer_Returns401()
    {
        var token = TestTokens.Create("someone-else", TestTokens.SigningKey, "user-1", [ReadRole], []);

        var response = await ClientWith(token).GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithUserIssuerSignedByServiceKey_Returns401()
    {
        var token = TestTokens.Create(TestTokens.Issuer, TestTokens.ServiceSigningKey, "user-1", [AdminRole], []);

        var response = await ClientWith(token).GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithoutReadRole_Returns403()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ClientWith(TestTokens.User(["Other.Role"], [tenantId])).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_OfAnotherTenant_Returns403()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ClientWith(TestTokens.User([ReadRole], [Guid.NewGuid()])).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_OfOwnTenant_Returns200()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ClientWith(TestTokens.User([ReadRole], [tenantId])).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithTrustedServiceToken_ReturnsTenant()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ClientWith(TestTokens.Service([ReadRole])).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_WithServiceTokenClaimingNotGrantedAdminRole_Returns403()
    {
        var response = await ClientWith(TestTokens.Service([ReadRole, AdminRole])).PostAsJsonAsync("/api/v1/tenants",
            new { name = "tenant", planId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithServiceTokenCarryingRolesOfOtherServices_ReturnsTenant()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ClientWith(TestTokens.Service([ReadRole, "Geospatial.Read"])).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithServiceTokenSignedByWrongKey_Returns401()
    {
        var token = TestTokens.Service([ReadRole], signingKey: "a-different-key-that-is-long-enough-0123456789");

        var response = await ClientWith(token).GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithRoleInLegacyClaimType_Returns200()
    {
        var tenantId = await CreateTenantAsync();
        var token = TestTokens.Create(TestTokens.Issuer, TestTokens.SigningKey, "user-1", [ReadRole], [tenantId],
            roleClaimType: ClaimTypes.Role);

        var response = await ClientWith(token).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_WithServiceTokenClaimingNotGrantedRoleInLegacyClaimType_Returns403()
    {
        var token = TestTokens.Create(TestTokens.ServiceIssuer, TestTokens.ServiceSigningKey, TestTokens.ServiceName,
            [AdminRole], tenants: null, roleClaimType: ClaimTypes.Role);

        var response = await ClientWith(token).GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_WithReadOnlyRole_Returns403()
    {
        var response = await ClientWith(TestTokens.User([ReadRole])).PostAsJsonAsync("/api/v1/tenants",
            new { name = "tenant", planId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_WithServiceToken_Returns403()
    {
        var response = await ClientWith(TestTokens.Service([ReadRole])).PostAsJsonAsync("/api/v1/tenants",
            new { name = "tenant", planId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterUsage_ForAnotherTenant_Returns403()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ClientWith(TestTokens.User(["TenantsBilling.Write"], [Guid.NewGuid()]))
            .PostAsJsonAsync($"/api/v1/tenants/{tenantId}/usage",
                new { eventsConsumed = 1, alertsTriggered = 0, apiRequests = 0, channelUsage = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
