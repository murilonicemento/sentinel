using AlertOrchestrator.Domain.Enums;

namespace AlertOrchestrator.Domain.Events;

public sealed record AlertEscalatedEvent(
    Guid AlertWindowId,
    string Region,
    string RiskType,
    AlertEscalationLevel EscalationLevel,
    DateTime EscalatedAt
);