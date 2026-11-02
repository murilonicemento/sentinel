using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CreateRegionalRiskParameterDTO
{
    /// <summary>
    /// Region identity (RC-14), unique ignoring case: required in v1; the legacy route generates one when absent.
    /// </summary>
    public string? RegionCode { get; set; }

    [Required] public double AdjustmentFactor { get; set; }
    [Required] public string Description { get; set; } = string.Empty;
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
    public PolygonDTO? RegionBounds { get; set; }
    public double? CoverageRadiusKm { get; set; }
}