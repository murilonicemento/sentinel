namespace Geospatial.Application.Models;

public class GeospatialOperationEvent
{
    public Guid EventId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public object? Payload { get; set; }
    public object? Result { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public GeoLocation? MainPoint { get; set; }
}

public class GeoLocation
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}
