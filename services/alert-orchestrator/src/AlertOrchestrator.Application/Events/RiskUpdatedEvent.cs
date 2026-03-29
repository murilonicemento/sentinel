using MediatR;

namespace AlertOrchestrator.Application.Events;

public sealed record RiskUpdatedEvent(
    Guid EventId,
    string Region,
    string RiskType,
    double RiskScore,
    string? TenantId,
    DateTime Timestamp,
    Dictionary<string, string> Metadata
) : INotification;
