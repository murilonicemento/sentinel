using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record RiskMatrixDTO
{
    [Required] public string EventTypeCode { get; set; }
    [Required] public string SeverityLevel { get; set; }
    [Required] public string RiskLevel { get; set; }
    [Required] public int Version { get; set; }
}