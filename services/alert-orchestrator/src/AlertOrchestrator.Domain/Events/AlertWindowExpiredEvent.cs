namespace AlertOrchestrator.Domain.Events;

public sealed record AlertWindowExpiredEvent(
    Guid WindowId,
    string Region,
    string RiskType,
    DateTime ExpiredAt,
    int SignalCount
);