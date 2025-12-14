using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;

namespace Ingestion.Application.DTO;

public record SampleSensorDTO
{
    [Required] public double SensorValue { get; set; }
    [Required] public string Unit { get; set; }
    [Required] public double Latitude { get; set; }
    [Required] public double Longitude { get; set; }
    [Required] [FutureDate] public DateTime RecordedAt { get; set; }
}