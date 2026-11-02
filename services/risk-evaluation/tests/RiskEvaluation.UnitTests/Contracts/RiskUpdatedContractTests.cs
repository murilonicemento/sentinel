using System.Text.Json;
using RiskEvaluation.Application.Contracts;
using Xunit;

namespace RiskEvaluation.UnitTests.Contracts;

/// <summary>
/// risk-updated is read by alert-orchestrator with System.Text.Json defaults (PascalCase, case-sensitive); the same
/// sample is asserted on its side in EventConsumerPollingTests.
/// </summary>
public class RiskUpdatedContractTests
{
    [Fact]
    public void Serialization_UsesTheNamesAlertOrchestratorReads()
    {
        var json = JsonSerializer.Serialize(new RiskUpdatedEvent(Guid.Parse("8f3a5c1e-2b4d-4e6f-9a1b-3c5d7e9f1a2b"),
            "rj-capital", "flood", 0.84, "6d3f1c8e-1a52-4b0e-9a59-2f0d6f1b9a01",
            new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), new Dictionary<string, string> { ["level"] = "Critical" }));

        Assert.Equal(
            "{\"EventId\":\"8f3a5c1e-2b4d-4e6f-9a1b-3c5d7e9f1a2b\",\"Region\":\"rj-capital\",\"RiskType\":\"flood\"," +
            "\"RiskScore\":0.84,\"TenantId\":\"6d3f1c8e-1a52-4b0e-9a59-2f0d6f1b9a01\"," +
            "\"Timestamp\":\"2026-11-01T12:00:00Z\",\"Metadata\":{\"level\":\"Critical\"}}", json);
    }
}
