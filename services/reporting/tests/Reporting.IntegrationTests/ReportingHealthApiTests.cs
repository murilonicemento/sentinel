using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reporting.IntegrationTests;

public class ReportingHealthApiTests : IClassFixture<WebApplicationFactory<Reporting.Api.Program>>
{
    private readonly WebApplicationFactory<Reporting.Api.Program> _factory;

    public ReportingHealthApiTests(WebApplicationFactory<Reporting.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reporting/health?tenantId=tenant-a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
