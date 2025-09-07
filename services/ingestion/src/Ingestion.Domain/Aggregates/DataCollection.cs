using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Aggregates;

public class DataCollection(Guid id, Guid dataSourceId, DateTime collectedAt, string payload)
{
    public Guid Id { get; } = id;
    public Guid DataSourceId { get; } = dataSourceId;
    public DateTime CollectedAt { get; } = collectedAt;
    public string Payload { get; } = payload;
    public DataSource? DataSource { get; set; }
    public IEnumerable<SampleSensor> SampleSensors { get; set; } = [];
}