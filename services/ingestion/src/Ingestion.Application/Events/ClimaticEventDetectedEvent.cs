using Ingestion.Domain.Enums;

namespace Ingestion.Application.Events;

public record ClimaticEventDetectedEvent(
    Guid EventId,
    string EventType,
    double Intensity,
    double Latitude,
    double Longitude,
    DateTime CollectedAt
);