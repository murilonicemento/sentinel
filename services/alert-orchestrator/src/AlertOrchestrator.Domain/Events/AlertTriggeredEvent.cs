namespace AlertOrchestrator.Domain.Events;

public sealed record AlertTriggeredEvent(
    Guid WindowId,
    string Region,
    string RiskType,
    DateTime TriggeredAt,
    double FinalRiskScore,
    int SignalCount,
    List<string> SignalSources
);