namespace Geospatial.Infrastructure.Messaging.Events;

public record ClimaticEventDetectedEvent(
    Guid EventId,
    string EventType,
    double Intensity,
    double Latitude,
    double Longitude,
    DateTime CollectedAt);