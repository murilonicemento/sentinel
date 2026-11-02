using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TenantsBilling.IntegrationTests.Support;

namespace TenantsBilling.IntegrationTests;

/// <summary>Tenant status changes and the tenant and plan listings (TB-12).</summary>
public class TenantsBillingV1LifecycleTests : IClassFixture<TenantsBillingApiFactory>
{
    private const string BasePath = "/api/v1/tenants-billing";
    private const string AdminRole = "TenantsBilling.Admin";
    private const string ReadRole = "TenantsBilling.Read";
    private const string WriteRole = "TenantsBilling.Write";

    private readonly TenantsBillingApiFactory _factory;

    public TenantsBillingV1LifecycleTests(TenantsBillingApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient AdminOf(params Guid[] tenants) => ClientWith(TestTokens.User([AdminRole], tenants));

    private async Task<Guid> CreatePlanAsync(string? name = null)
    {
        var response = await AdminOf().PostAsJsonAsync($"{BasePath}/plans", new
        {
            name = name ?? $"plan-{Guid.NewGuid():N}",
            maxEventsPerMonth = 100,
            maxAlertsPerMonth = 10,
            maxApiRequestsPerMonth = 100,
            maxChannelsPerMonth = 5
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTenantAsync(string? name = null)
    {
        var response = await AdminOf().PostAsJsonAsync($"{BasePath}/tenants",
            new { name = name ?? $"tenant-{Guid.NewGuid():N}", planId = await CreatePlanAsync() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, Guid tenantId, string? status) =>
        client.PatchAsJsonAsync($"{BasePath}/tenants/{tenantId}/status", new { status });

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task ChangeStatus_SuspendsReactivatesAndCancels()
    {
        var tenantId = await CreateTenantAsync();
        var admin = AdminOf(tenantId);

        foreach (var status in new[] { "Suspended", "active", "Canceled" })
        {
            var response = await ChangeStatusAsync(admin, tenantId, status);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(200, body.GetProperty("statusCode").GetInt32());
            Assert.Equal(char.ToUpperInvariant(status[0]) + status[1..],
                body.GetProperty("data").GetProperty("status").GetString());
        }

        var read = await ClientWith(TestTokens.User([ReadRole], [tenantId])).GetAsync($"{BasePath}/tenants/{tenantId}");
        Assert.Equal("Canceled",
            (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ChangeStatus_ToTheCurrentStatus_ReturnsTheTenantUnchanged()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ChangeStatusAsync(AdminOf(tenantId), tenantId, "Active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_OfACanceledTenant_IsConflict()
    {
        var tenantId = await CreateTenantAsync();
        var admin = AdminOf(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ChangeStatusAsync(admin, tenantId, "Canceled")).StatusCode);

        var response = await ChangeStatusAsync(admin, tenantId, "Active");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TENANT_STATUS_TRANSITION_NOT_ALLOWED", await ProblemCodeAsync(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Pending")]
    [InlineData("2")]
    [InlineData("Deleted")]
    public async Task ChangeStatus_WithAnUnsupportedStatus_IsValidationError(string? status)
    {
        var tenantId = await CreateTenantAsync();

        var response = await ChangeStatusAsync(AdminOf(tenantId), tenantId, status);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task ChangeStatus_RequiresTheAdminRole()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ChangeStatusAsync(ClientWith(TestTokens.User([WriteRole], [tenantId])), tenantId,
            "Suspended");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_OfAnotherTenant_IsDenied()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ChangeStatusAsync(AdminOf(Guid.NewGuid()), tenantId, "Suspended");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_ACCESS_DENIED", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task ChangeStatus_OfAnUnknownTenant_IsNotFound()
    {
        var tenantId = Guid.NewGuid();

        var response = await ChangeStatusAsync(AdminOf(tenantId), tenantId, "Suspended");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TENANT_NOT_FOUND", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task ChangeStatus_WithAServiceToken_IsForbidden()
    {
        var tenantId = await CreateTenantAsync();

        var response = await ChangeStatusAsync(ClientWith(TestTokens.Service([AdminRole])), tenantId, "Suspended");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListTenants_ReturnsOnlyTheIdentitysTenantsPagedByName()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var bravo = await CreateTenantAsync($"{prefix}-bravo");
        var alpha = await CreateTenantAsync($"{prefix}-alpha");
        var charlie = await CreateTenantAsync($"{prefix}-charlie");
        await CreateTenantAsync($"{prefix}-other");
        var reader = ClientWith(TestTokens.User([ReadRole], [bravo, alpha, charlie]));

        var first = (await reader.GetFromJsonAsync<JsonElement>($"{BasePath}/tenants?page=1&pageSize=2"))
            .GetProperty("data");
        var second = (await reader.GetFromJsonAsync<JsonElement>($"{BasePath}/tenants?page=2&pageSize=2"))
            .GetProperty("data");

        Assert.Equal([alpha, bravo], Ids(first));
        Assert.Equal([charlie], Ids(second));
        Assert.Equal(3, first.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListTenants_FiltersByStatus()
    {
        var active = await CreateTenantAsync();
        var suspended = await CreateTenantAsync();
        Assert.Equal(HttpStatusCode.OK, (await ChangeStatusAsync(AdminOf(suspended), suspended, "Suspended")).StatusCode);
        var reader = ClientWith(TestTokens.User([ReadRole], [active, suspended]));

        var data = (await reader.GetFromJsonAsync<JsonElement>($"{BasePath}/tenants?status=suspended"))
            .GetProperty("data");

        Assert.Equal([suspended], Ids(data));
    }

    [Fact]
    public async Task ListTenants_WithoutTenantClaims_IsEmpty()
    {
        await CreateTenantAsync();

        var data = (await ClientWith(TestTokens.User([ReadRole])).GetFromJsonAsync<JsonElement>($"{BasePath}/tenants"))
            .GetProperty("data");

        Assert.Empty(Ids(data));
        Assert.Equal(0, data.GetProperty("totalItems").GetInt64());
    }

    [Fact]
    public async Task ListTenants_ForATrustedService_IncludesEveryTenant()
    {
        var tenantId = await CreateTenantAsync($"!{Guid.NewGuid():N}");

        var data = (await ClientWith(TestTokens.Service([ReadRole]))
            .GetFromJsonAsync<JsonElement>($"{BasePath}/tenants?pageSize=50")).GetProperty("data");

        Assert.Contains(tenantId, Ids(data));
    }

    [Theory]
    [InlineData("tenants?page=0")]
    [InlineData("tenants?pageSize=51")]
    [InlineData("tenants?status=Deleted")]
    [InlineData("tenants?status=1")]
    [InlineData("plans?pageSize=0")]
    public async Task Listings_WithInvalidParameters_AreValidationErrors(string query)
    {
        var response = await ClientWith(TestTokens.User([ReadRole])).GetAsync($"{BasePath}/{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task ListPlans_ReturnsPlansOrderedByName()
    {
        var prefix = $"!{Guid.NewGuid():N}";
        var second = await CreatePlanAsync($"{prefix}-b");
        var first = await CreatePlanAsync($"{prefix}-a");

        var data = (await ClientWith(TestTokens.User([ReadRole])).GetFromJsonAsync<JsonElement>(
            $"{BasePath}/plans?pageSize=50")).GetProperty("data");

        var ids = Ids(data);
        Assert.True(ids.IndexOf(first) >= 0 && ids.IndexOf(first) < ids.IndexOf(second));
        Assert.Equal(10, (await ClientWith(TestTokens.User([ReadRole])).GetFromJsonAsync<JsonElement>(
            $"{BasePath}/plans")).GetProperty("data").GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Listings_RequireTheReadRole()
    {
        var client = ClientWith(TestTokens.User([WriteRole]));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{BasePath}/tenants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{BasePath}/plans")).StatusCode);
    }

    private static List<Guid> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
}
