using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;

namespace Ingestion.Application.DTO;

public record SampleSensorDTO(
    double SensorValue,
    string Unit,
    double Latitude,
    double Longitude,
    DateTime RecordedAt
)
{
    [Required] public double SensorValue { get; } = SensorValue;
    [Required] public string Unit { get; } = Unit;
    [Required] public double Latitude { get; } = Latitude;
    [Required] public double Longitude { get; } = Longitude;
    [Required] [FutureDate] public DateTime RecordedAt { get; } = RecordedAt;
}