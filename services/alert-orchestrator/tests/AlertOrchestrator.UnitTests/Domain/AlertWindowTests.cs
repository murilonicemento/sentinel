using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Configuration;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Events;
using AlertOrchestrator.Domain.ValueObjects;
using Xunit;

namespace AlertOrchestrator.UnitTests.Domain;

public class AlertWindowTests
{
    [Fact]
    public void AddSignal_ShouldIgnoreDuplicateEvent()
    {
        var window = AlertWindow.Open(
            "region-1",
            RiskType.Flood,
            "tenant-1",
            100,
            TimeSpan.FromHours(1));

        var signal = new Signal(
            SignalSource.Sensor,
            DateTime.UtcNow,
            Guid.NewGuid(),
            150,
            RiskType.Flood,
            new Dictionary<string, string>());

        window.AddSignal(signal);
        window.AddSignal(signal);

        Assert.Single(window.Signals);
    }

    [Fact]
    public void TryTrigger_WhenQuorumAndThresholdMet_ShouldMarkWindowTriggered()
    {
        var window = AlertWindow.Open(
            "region-1",
            RiskType.Flood,
            "tenant-1",
            100,
            TimeSpan.FromHours(1));

        var signal = new Signal(
            SignalSource.Sensor,
            DateTime.UtcNow,
            Guid.NewGuid(),
            150,
            RiskType.Flood,
            new Dictionary<string, string>());

        window.AddSignal(signal);

        var quorum = new QuorumConfiguration(
            MinimumSignals: 1,
            RequiredDistinctSources: 1,
            RequireSensor: false,
            RequireSatellite: false);

        var result = window.TryTrigger(150, quorum);

        Assert.True(result);
        Assert.Equal(AlertStatus.Triggered, window.Status);
        Assert.Equal(150, window.FinalRiskScore);
        Assert.NotNull(window.TriggeredAt);
        Assert.Contains(window.DomainEvents, e => e is AlertTriggeredEvent);
    }
}
