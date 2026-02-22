namespace Geospatial.Infrastructure.Messaging.Events;

public record SensorEventDetected(
    Guid CollectionId,
    string EventType,
    double Intensity,
    double Latitude,
    double Longitude,
    DateTime CollectedAt);