using System.Net;
using System.Net.Http.Json;
using Reporting.Domain.Entities;

using Reporting.IntegrationTests.Support;

namespace Reporting.IntegrationTests;

public class ReportingEventEnvelopeTests : IClassFixture<ReportingApiFactory>
{
    private readonly ReportingApiFactory _factory;

    public ReportingEventEnvelopeTests(ReportingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostEnvelope_AcceptsAndProcessesEvent()
    {
        using var client = _factory.CreateWriterClient("tenant-c");
        var envelope = new ReportingEventEnvelope("evt-10", "AlertTriggered", "tenant-c", "west", "High", "sms", 0.9, "Success", DateTime.UtcNow, "alert-orchestrator");

        var response = await client.PostAsJsonAsync("/api/reporting/events/envelope", envelope);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
