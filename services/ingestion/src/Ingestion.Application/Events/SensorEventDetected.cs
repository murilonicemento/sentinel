using Ingestion.Domain.Enums;

namespace Ingestion.Application.Events;

/// <summary>
/// Payload of <c>climatic-event-detected</c> / <c>disaster-event-detected</c>. <c>EventId</c> is deterministic for a
/// given collection sample, so a retried collection republishes the same id and consumers can discard duplicates.
/// </summary>
public record SensorEventDetected(
    Guid CollectionId,
    string EventType,
    double Intensity,
    double Latitude,
    double Longitude,
    DateTime CollectedAt,
    Guid EventId
);