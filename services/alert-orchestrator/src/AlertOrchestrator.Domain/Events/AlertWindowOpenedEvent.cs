namespace AlertOrchestrator.Domain.Events;

public sealed record AlertWindowOpenedEvent(
    Guid WindowId,
    string Region,
    string RiskType,
    DateTime OpenedAt,
    DateTime ExpiresAt,
    double Threshold
);
