using AlertOrchestrator.Domain.Enums;

namespace AlertOrchestrator.Domain.ValueObjects;

public sealed record Signal(
    SignalSource Source,
    DateTime Timestamp,
    Guid EventId,
    double RiskScore,
    RiskType RiskType,
    Dictionary<string, string> Metadata
);
