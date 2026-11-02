using System.Text.Json;
using AlertOrchestrator.Application.Events;

namespace AlertOrchestrator.UnitTests.Contracts;

/// <summary>
/// <c>reporting-events</c> as reporting reads it (X-24). The same sample is read by reporting's
/// <c>ProducerContractTests</c>.
/// </summary>
public class ReportingEventContractTests
{
    private const string Sample = """
        {"EventId":"alert-triggered:3c9a1f0e5b7d4e2a8f6c1d0b9a8e7f6d","EventType":"AlertTriggered","TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","Region":"rj-capital","Severity":"HIGH","Channel":"none","RiskScore":0.84,"Status":"SUCCESS","Timestamp":"2026-11-01T12:00:00Z","Source":"alert-orchestrator"}
        """;

    [Fact]
    public void TriggeredAlert_SerializesToTheContract()
    {
        var reportingEvent = ReportingEventEnvelope.ForTriggeredAlert("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f",
            Guid.Parse("3c9a1f0e-5b7d-4e2a-8f6c-1d0b9a8e7f6d"), "rj-capital", 0.84,
            new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(Sample.Trim(), JsonSerializer.Serialize(reportingEvent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void TriggeredAlert_WithoutATenant_IsNotReported(string? tenantId)
    {
        Assert.Null(ReportingEventEnvelope.ForTriggeredAlert(tenantId, Guid.NewGuid(), "rj-capital", 0.84,
            DateTime.UtcNow));
    }
}
