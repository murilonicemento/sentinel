namespace AlertOrchestrator.Domain.Events;

public sealed record AlertClosedEvent(
    Guid AlertWindowId,
    string Region,
    string RiskType,
    DateTime ClosedAt,
    int TotalSignals,
    string? ClosedBy,
    string? Reason
);