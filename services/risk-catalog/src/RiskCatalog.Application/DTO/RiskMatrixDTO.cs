using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record RiskMatrixDTO
{
    [Required] public string EventTypeCode { get; set; } = string.Empty;
    [Required] public string SeverityLevel { get; set; } = string.Empty;
    [Required] public string RiskLevel { get; set; } = string.Empty;
    [Required] public int Version { get; set; }
}