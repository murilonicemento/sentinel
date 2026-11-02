using System.Net;
using Reporting.Domain.Entities;

using Reporting.IntegrationTests.Support;

namespace Reporting.IntegrationTests;

public class ReportingAnalyticsApiTests : IClassFixture<ReportingApiFactory>
{
    private readonly ReportingApiFactory _factory;

    public ReportingAnalyticsApiTests(ReportingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAnalytics_ReturnsOkWithMetrics()
    {
        using var client = _factory.CreateReaderClient("tenant-a");

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/analytics?region=north");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalytics_WithFullFilters_ReturnsOk()
    {
        using var client = _factory.CreateReaderClient("tenant-a");

        var response = await client.GetAsync("/api/reporting/tenants/tenant-a/analytics?region=north&eventType=AlertTriggered&severity=High&channel=sms&status=Success&startDate=2026-07-01T00:00:00Z&endDate=2026-07-31T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
