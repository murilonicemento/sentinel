using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Aggregates;

public class DataCollection
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DateTime CollectedAt { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DataSource DataSource { get; set; } = new();
    public ICollection<SampleSensor> SampleSensors { get; set; } = [];
}