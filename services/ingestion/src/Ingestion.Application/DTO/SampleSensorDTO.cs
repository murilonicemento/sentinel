namespace Ingestion.Application.DTO;

public record SampleSensorDTO(
    double SensorValue,
    string Unit,
    DateTime RecordedAt
);