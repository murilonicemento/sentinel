using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Aggregates;

public class DataCollection
{
    public Guid Id { get; }
    public Guid DataSourceId { get; }
    public DateTime CollectedAt { get; }
    public string Payload { get; } = string.Empty;
    public Guid TenantId { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public IEnumerable<SampleSensor> SampleSensors { get; set; } = [];

    public DataCollection()
    {
    }

    public DataCollection(
        Guid id,
        Guid dataSourceId,
        DateTime collectedAt,
        string payload,
        Guid tenantId
    )
    {
        Id = id;
        DataSourceId = dataSourceId;
        CollectedAt = collectedAt;
        Payload = payload;
        TenantId = tenantId;
    }
}