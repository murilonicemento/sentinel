using Ingestion.Domain.Enums;

namespace Ingestion.Application.Events;

public record SensorEventDetected(
    Guid CollectionId,
    string EventType,
    double Intensity,
    double Latitude,
    double Longitude,
    DateTime CollectedAt
);