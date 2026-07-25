using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Reporting.Domain.Entities;

namespace Reporting.IntegrationTests;

public class ReportingEventEnvelopeTests : IClassFixture<WebApplicationFactory<Reporting.Api.Program>>
{
    private readonly WebApplicationFactory<Reporting.Api.Program> _factory;

    public ReportingEventEnvelopeTests(WebApplicationFactory<Reporting.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostEnvelope_AcceptsAndProcessesEvent()
    {
        using var client = _factory.CreateClient();
        var envelope = new ReportingEventEnvelope("evt-10", "AlertTriggered", "tenant-c", "west", "High", "sms", 0.9, "Success", DateTime.UtcNow, "alert-orchestrator");

        var response = await client.PostAsJsonAsync("/api/reporting/events/envelope", envelope);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
