using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.IntegrationTests.Support;

namespace Reporting.IntegrationTests;

public class ReportingV1ContractTests : IClassFixture<ReportingApiFactory>
{
    private const string BasePath = "/api/v1/reporting";

    private readonly ReportingApiFactory _factory;

    public ReportingV1ContractTests(ReportingApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task PostEvent_ThenSummary_ReturnsDtoEnvelopes()
    {
        var tenant = $"tenant-{Guid.NewGuid():N}";
        var post = await _factory.CreateWriterClient(tenant).PostAsJsonAsync($"{BasePath}/events", new
        {
            eventId = "evt-v1",
            eventType = "AlertTriggered",
            tenantId = tenant,
            region = "north",
            severity = "High",
            channel = "sms",
            riskScore = 0.8,
            status = "Success"
        });
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);
        Assert.Equal("evt-v1", (await post.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("eventId").GetString());

        var summary = await _factory.CreateReaderClient(tenant).GetFromJsonAsync<JsonElement>(
            $"{BasePath}/tenants/{tenant}/summary");

        Assert.True(summary.GetProperty("success").GetBoolean());
        Assert.Equal(tenant, summary.GetProperty("data").GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task Analytics_ReturnsBreakdownsAsKeyCountPairs()
    {
        var body = await _factory.CreateReaderClient("tenant-a").GetFromJsonAsync<JsonElement>(
            $"{BasePath}/tenants/tenant-a/analytics?region=north");

        var data = body.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.GetProperty("regions").ValueKind);
        Assert.Equal(JsonValueKind.Array, data.GetProperty("severities").ValueKind);
    }

    [Fact]
    public async Task Analytics_WithStartAfterEnd_ReturnsFieldError()
    {
        var response = await _factory.CreateReaderClient("tenant-a").GetAsync(
            $"{BasePath}/tenants/tenant-a/analytics?startDate=2026-08-01T00:00:00Z&endDate=2026-07-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("startDate", out _));
    }

    [Fact]
    public async Task Export_WithUnsupportedFormat_ReturnsFieldError()
    {
        var response = await _factory.CreateReaderClient("tenant-a").GetAsync(
            $"{BasePath}/tenants/tenant-a/exports?format=xml");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("format", out _));
    }

    [Fact]
    public async Task Export_AsJson_ReturnsJsonFile()
    {
        var response = await _factory.CreateReaderClient("tenant-a").GetAsync(
            $"{BasePath}/tenants/tenant-a/exports?format=json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostEvent_WithoutEventId_ReturnsValidationProblem()
    {
        var response = await _factory.CreateWriterClient("tenant-a").PostAsJsonAsync($"{BasePath}/events",
            new { eventType = "AlertTriggered", tenantId = "tenant-a" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Summary_OfAnotherTenant_ReturnsTenantAccessDenied()
    {
        var response = await _factory.CreateReaderClient("tenant-b").GetAsync($"{BasePath}/tenants/tenant-a/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_ACCESS_DENIED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ProcessingStatus_ReturnsStatusForTenant()
    {
        var body = await _factory.CreateReaderClient("tenant-a").GetFromJsonAsync<JsonElement>(
            $"{BasePath}/tenants/tenant-a/processing-status");

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("data").GetProperty("status").GetString()));
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecation()
    {
        var response = await _factory.CreateReaderClient("tenant-a").GetAsync("/api/reporting/tenants/tenant-a/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Contains("/api/v1/reporting", response.Headers.GetValues("Link").Single());
    }
}
