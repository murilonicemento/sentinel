namespace Geospatial.Domain.Events;

public class GeospatialOperationEvent
{
    public Guid CollectionId { get; set; }

    /// <summary>
    /// Id of the source event when the producer sends one. Samples of one collection share <see cref="CollectionId"/>,
    /// so this keeps each sample as its own document.
    /// </summary>
    public Guid? SourceEventId { get; set; }

    public string OperationType { get; set; } = string.Empty;
    public object? Payload { get; set; }
    public object? Result { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public GeoLocation? MainPoint { get; set; }
}
