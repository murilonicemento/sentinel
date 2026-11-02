using System.Text.Json;
using AlertOrchestrator.Application.Commands;
using AlertOrchestrator.Domain.Enums;

namespace AlertOrchestrator.UnitTests.Contracts;

/// <summary>
/// alert-commands is read by channels (TriggerAlertCommandDTO, case-insensitive); it needs these names, the tenant
/// and the escalation level as a number.
/// </summary>
public class TriggerAlertCommandContractTests
{
    [Fact]
    public void Serialization_CarriesWhatChannelsReads()
    {
        var windowId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new TriggerAlertCommand(windowId, "rj-capital", "flood", 0.9, 2,
            ["Sensor", "Sensor"], DateTime.UtcNow, AlertEscalationLevel.Level2, TenantId: "tenant-a"));

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(windowId, root.GetProperty("AlertWindowId").GetGuid());
        Assert.Equal("rj-capital", root.GetProperty("Region").GetString());
        Assert.Equal("flood", root.GetProperty("RiskType").GetString());
        Assert.Equal(0.9, root.GetProperty("RiskScore").GetDouble());
        Assert.Equal(2, root.GetProperty("SignalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("EscalationLevel").GetInt32());
        Assert.Equal("tenant-a", root.GetProperty("TenantId").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("TriggeredAt").ValueKind);
    }
}
