namespace Ingestion.Domain.Aggregates;

public class EventTypePermission
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public string EventDomain { get; set; } // "Climatic" ou "Disaster"
    public string EventType { get; set; } // "TemperatureAnomaly", "WindGust", "Wildfire", etc
}