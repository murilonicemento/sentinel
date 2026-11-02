using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;

namespace Ingestion.Application.DTO;

public record SampleSensorDTO
{
    [Required] public double SensorValue { get; set; }
    [Required] public string Unit { get; set; }
    [Required] [Range(-90, 90)] public double Latitude { get; set; }
    [Required] [Range(-180, 180)] public double Longitude { get; set; }
    [Required] [NotInFuture] public DateTime RecordedAt { get; set; }
}