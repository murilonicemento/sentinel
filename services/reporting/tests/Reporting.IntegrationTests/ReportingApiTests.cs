using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Reporting.Domain.Entities;

namespace Reporting.IntegrationTests;

public class ReportingApiTests : IClassFixture<WebApplicationFactory<Reporting.Api.Program>>
{
    private readonly WebApplicationFactory<Reporting.Api.Program> _factory;

    public ReportingApiTests(WebApplicationFactory<Reporting.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSummary_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_AcceptsPayload()
    {
        using var client = _factory.CreateClient();
        var payload = new ReportingEvent("evt-3", "AlertTriggered", "tenant-b", "south", "Medium", "push", 0.65, "Success");

        var response = await client.PostAsJsonAsync("/api/reporting/events", payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
