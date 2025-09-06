namespace Ingestion.Domain.Aggregates;

public class SampleSensor
{
    public Guid Id { get; set; }
    public Guid DataCollectionId { get; set; }
    public double SensorValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public DataCollection DataCollection { get; set; } = new();
}