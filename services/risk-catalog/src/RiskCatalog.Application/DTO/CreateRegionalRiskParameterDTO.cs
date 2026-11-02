using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CreateRegionalRiskParameterDTO
{
    [Required] public double AdjustmentFactor { get; set; }
    [Required] public string Description { get; set; }
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
    public PolygonDTO? RegionBounds { get; set; }
    public double? CoverageRadiusKm { get; set; }
}