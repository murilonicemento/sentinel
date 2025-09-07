namespace Ingestion.Domain.Aggregates;

public class SampleSensor(Guid id, Guid dataCollectionId, double sensorValue, string unit, DateTime recordedAt)
{
    public Guid Id { get; set; } = id;
    public Guid DataCollectionId { get; set; } = dataCollectionId;
    public double SensorValue { get; set; } = sensorValue;
    public string Unit { get; set; } = unit;
    public DateTime RecordedAt { get; set; } = recordedAt;
    public DataCollection? DataCollection { get; set; }
}