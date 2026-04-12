using MediatR;

namespace AlertOrchestrator.Application.Events;

public sealed record RegionIntersectedEvent(
    Guid EventId,
    string Region,
    string IntersectingRegion,
    string RiskType,
    double Severity,
    DateTime Timestamp
) : INotification;