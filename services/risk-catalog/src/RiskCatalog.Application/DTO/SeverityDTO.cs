using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record SeverityDTO
{
    [Required] public string Level { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
}