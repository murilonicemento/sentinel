using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record RegionalRiskParametersDTO
{
    [Required] public Guid RegionId { get; set; }
    [Required] public double AdjustmentFactor { get; set; }
    [Required] public string Description { get; set; }
}