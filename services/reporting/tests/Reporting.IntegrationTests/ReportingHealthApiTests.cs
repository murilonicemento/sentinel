using System.Net;

using Reporting.IntegrationTests.Support;

namespace Reporting.IntegrationTests;

public class ReportingHealthApiTests : IClassFixture<ReportingApiFactory>
{
    private readonly ReportingApiFactory _factory;

    public ReportingHealthApiTests(ReportingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        using var client = _factory.CreateReaderClient("tenant-a");

        var response = await client.GetAsync("/api/reporting/health?tenantId=tenant-a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
