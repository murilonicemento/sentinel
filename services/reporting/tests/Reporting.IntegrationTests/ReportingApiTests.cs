using System.Net;
using System.Net.Http.Json;
using Reporting.Domain.Entities;

using Reporting.IntegrationTests.Support;

namespace Reporting.IntegrationTests;

public class ReportingApiTests : IClassFixture<ReportingApiFactory>
{
    private readonly ReportingApiFactory _factory;

    public ReportingApiTests(ReportingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSummary_ReturnsOk()
    {
        using var client = _factory.CreateReaderClient("tenant-a");

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_AcceptsPayload()
    {
        using var client = _factory.CreateWriterClient("tenant-b");
        var payload = new ReportingEvent("evt-3", "AlertTriggered", "tenant-b", "south", "Medium", "push", 0.65, "Success");

        var response = await client.PostAsJsonAsync("/api/reporting/events", payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClientWithToken(null);

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_OfAnotherTenant_Returns403()
    {
        using var client = _factory.CreateReaderClient("tenant-b");

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_OfAnotherTenant_Returns403()
    {
        using var client = _factory.CreateReaderClient("tenant-b");

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/export?format=json");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_ForAnotherTenant_Returns403()
    {
        using var client = _factory.CreateWriterClient("tenant-a");
        var payload = new ReportingEvent("evt-4", "AlertTriggered", "tenant-b", "south", "Medium", "push", 0.65, "Success");

        var response = await client.PostAsJsonAsync("/api/reporting/events", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_WithReadOnlyRole_Returns403()
    {
        using var client = _factory.CreateReaderClient("tenant-b");
        var payload = new ReportingEvent("evt-5", "AlertTriggered", "tenant-b", "south", "Medium", "push", 0.65, "Success");

        var response = await client.PostAsJsonAsync("/api/reporting/events", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Replay_WithoutAdminRole_Returns403()
    {
        using var client = _factory.CreateWriterClient("tenant-a");

        var response = await client.PostAsJsonAsync("/api/reporting/events/replay",
            new { sourceTopic = "reporting-events-dead-letter", payload = "{}" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PrometheusMetrics_AreAnonymous()
    {
        using var client = _factory.CreateClientWithToken(null);

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        using var client = _factory.CreateClientWithToken(null);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
