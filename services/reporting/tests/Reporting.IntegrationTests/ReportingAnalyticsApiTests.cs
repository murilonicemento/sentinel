using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Reporting.Domain.Entities;

namespace Reporting.IntegrationTests;

public class ReportingAnalyticsApiTests : IClassFixture<WebApplicationFactory<Reporting.Api.Program>>
{
    private readonly WebApplicationFactory<Reporting.Api.Program> _factory;

    public ReportingAnalyticsApiTests(WebApplicationFactory<Reporting.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAnalytics_ReturnsOkWithMetrics()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/analytics?region=north");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalytics_WithFullFilters_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/analytics?region=north&eventType=AlertTriggered&severity=High&channel=sms&status=Success&startDate=2026-07-01T00:00:00Z&endDate=2026-07-31T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
