namespace Ingestion.Domain.Aggregates;

public class EventTypePermission
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public string EventDomain { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
}