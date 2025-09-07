using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;

namespace Ingestion.Application.DTO;

public record SampleSensorDTO
{
    [Required] public double SensorValue { get; set; }
    [Required] public string Unit { get; set; } = string.Empty;
    [Required] [FutureDate] public DateTime RecordedAt { get; set; }
}