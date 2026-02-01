using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record SeverityCriterionDTO
{
    [Required] public string EventTypeCode { get; set; }
    [Required] public string SeverityLevel { get; set; }
    [Required] public double MinValue { get; set; }
    [Required] public double MaxValue { get; set; }
    [Required] public string Unit { get; set; }
    [Required] public int Version { get; set; }
}