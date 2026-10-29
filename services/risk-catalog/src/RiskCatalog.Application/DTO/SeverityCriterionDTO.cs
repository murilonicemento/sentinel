using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record SeverityCriterionDTO
{
    [Required] public string EventTypeCode { get; set; } = string.Empty;
    [Required] public string SeverityLevel { get; set; } = string.Empty;
    [Required] public double MinValue { get; set; }
    [Required] public double MaxValue { get; set; }
    [Required] public string Unit { get; set; } = string.Empty;
    [Required] public int Version { get; set; }
}