using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CreateIDFCurvesDTO
{
    [Required] public string EventTypeCode { get; set; }
    [Required] public int DurationMinutes { get; set; }
    [Required] public double Intensity { get; set; }
    [Required] public int ReturnPeriodYears { get; set; }
    [Required] public int Version { get; set; }
}