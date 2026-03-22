namespace Geospatial.Domain.Events;

public class GeospatialOperationEvent
{
    public Guid CollectionId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public object? Payload { get; set; }
    public object? Result { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public GeoLocation? MainPoint { get; set; }
}
