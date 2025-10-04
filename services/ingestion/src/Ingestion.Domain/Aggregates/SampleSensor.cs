namespace Ingestion.Domain.Aggregates;

public class SampleSensor
{
    public Guid Id { get; set; }
    public Guid DataCollectionId { get; set; }
    public double SensorValue { get; set; }
    public string Unit { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAt { get; set; }

    public SampleSensor()
    {
    }

    public SampleSensor(
        Guid id,
        Guid dataCollectionId,
        double sensorValue,
        string unit,
        double latitude,
        double longitude,
        DateTime recordedAt
    )
    {
        Id = id;
        DataCollectionId = dataCollectionId;
        SensorValue = sensorValue;
        Unit = unit;
        Latitude = latitude;
        Longitude = longitude;
        RecordedAt = recordedAt;
    }
}