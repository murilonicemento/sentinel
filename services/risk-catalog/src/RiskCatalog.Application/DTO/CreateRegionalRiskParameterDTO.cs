using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CreateRegionalRiskParameterDTO
{
    [Required] public double AdjustmentFactor { get; set; }
    [Required] public string Description { get; set; }
}