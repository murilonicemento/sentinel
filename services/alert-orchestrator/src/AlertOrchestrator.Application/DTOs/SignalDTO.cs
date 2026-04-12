namespace AlertOrchestrator.Application.DTOs;

public sealed record SignalDTO(
    string Source,
    DateTime Timestamp,
    Guid EventId,
    double RiskScore,
    string RiskType
);