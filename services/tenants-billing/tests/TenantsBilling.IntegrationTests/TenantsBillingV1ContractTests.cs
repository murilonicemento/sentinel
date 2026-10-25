using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TenantsBilling.IntegrationTests.Support;

namespace TenantsBilling.IntegrationTests;

public class TenantsBillingV1ContractTests : IClassFixture<TenantsBillingApiFactory>
{
    private const string BasePath = "/api/v1/tenants-billing";
    private const string AdminRole = "TenantsBilling.Admin";
    private const string ReadRole = "TenantsBilling.Read";

    private readonly TenantsBillingApiFactory _factory;

    public TenantsBillingV1ContractTests(TenantsBillingApiFactory factory)
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

    private HttpClient Admin() => ClientWith(TestTokens.User([AdminRole]));

    private async Task<Guid> CreatePlanAsync()
    {
        var response = await Admin().PostAsJsonAsync($"{BasePath}/plans", new
        {
            name = $"plan-{Guid.NewGuid():N}", maxEventsPerMonth = 100, maxAlertsPerMonth = 10,
            maxApiRequestsPerMonth = 100, maxChannelsPerMonth = 5
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> CreateTenantAsync(string? name = null)
    {
        var response = await Admin().PostAsJsonAsync($"{BasePath}/tenants",
            new { name = name ?? $"tenant-{Guid.NewGuid():N}", planId = await CreatePlanAsync() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task CreateTenant_ReturnsEnvelopeWithStringStatusAndLocation()
    {
        var body = await CreateTenantAsync();

        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal(201, body.GetProperty("statusCode").GetInt32());
        Assert.Equal("Active", body.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetTenant_ReturnsTenantDtoWithStringStatus()
    {
        var created = await CreateTenantAsync();
        var tenantId = created.GetProperty("data").GetProperty("id").GetGuid();

        var response = await ClientWith(TestTokens.User([ReadRole], [tenantId])).GetAsync($"{BasePath}/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(tenantId, data.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.String, data.GetProperty("status").ValueKind);
    }

    [Fact]
    public async Task GetTenant_WhenMissing_ReturnsProblemWithStableCode()
    {
        var response = await ClientWith(TestTokens.Service([ReadRole])).GetAsync($"{BasePath}/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("TENANT_NOT_FOUND", problem.GetProperty("code").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetTenant_OfAnotherTenant_ReturnsTenantAccessDenied()
    {
        var response = await ClientWith(TestTokens.User([ReadRole], [Guid.NewGuid()]))
            .GetAsync($"{BasePath}/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_ACCESS_DENIED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetTenant_WithoutToken_ReturnsProblemDetails401()
    {
        var response = await ClientWith(null).GetAsync($"{BasePath}/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateTenant_WithMissingFields_ReturnsFieldErrors()
    {
        var response = await Admin().PostAsJsonAsync($"{BasePath}/tenants", new { name = "", planId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("planId", out _));
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateName_Returns409()
    {
        var name = $"tenant-{Guid.NewGuid():N}";
        await CreateTenantAsync(name);

        var response = await Admin().PostAsJsonAsync($"{BasePath}/tenants", new { name, planId = await CreatePlanAsync() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TENANT_ALREADY_EXISTS", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateTenant_WithUnknownPlan_Returns422()
    {
        var response = await Admin().PostAsJsonAsync($"{BasePath}/tenants", new { name = "tenant", planId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PLAN_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreatePlan_WithOutOfRangeLimit_ReturnsFieldErrorWithoutInternalMessage()
    {
        var response = await Admin().PostAsJsonAsync($"{BasePath}/plans", new
        {
            name = "plan", maxEventsPerMonth = 0, maxAlertsPerMonth = 10, maxApiRequestsPerMonth = 100,
            maxChannelsPerMonth = 5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty("maxEventsPerMonth", out _));
        Assert.DoesNotContain("Specified argument", problem.GetRawText());
    }

    [Fact]
    public async Task RegisterUsage_WithNegativeValue_ReturnsValidationError()
    {
        var tenantId = (await CreateTenantAsync()).GetProperty("data").GetProperty("id").GetGuid();

        var response = await ClientWith(TestTokens.User(["TenantsBilling.Write"], [tenantId]))
            .PostAsJsonAsync($"{BasePath}/tenants/{tenantId}/usage",
                new { eventsConsumed = -1, alertsTriggered = 0, apiRequests = 0, channelUsage = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("eventsConsumed", out _));
    }

    [Fact]
    public async Task LegacyGetTenant_KeepsNumericStatusAndAnnouncesDeprecation()
    {
        var tenantId = (await CreateTenantAsync()).GetProperty("data").GetProperty("id").GetGuid();

        var response = await ClientWith(TestTokens.User([ReadRole], [tenantId])).GetAsync($"/api/v1/tenants/{tenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Number,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").ValueKind);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.True(response.Headers.Contains("Sunset"));
        Assert.Contains("rel=\"successor-version\"", string.Join(",", response.Headers.GetValues("Link")));
    }
}
