namespace AlertOrchestrator.Domain.Events;

public sealed record AlertConfirmedEvent(
    Guid AlertWindowId,
    string Region,
    string RiskType,
    DateTime ConfirmedAt
);