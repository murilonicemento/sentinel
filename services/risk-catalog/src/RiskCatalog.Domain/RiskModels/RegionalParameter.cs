using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Domain.RiskModels;

public class RegionalParameter
{
    [Key]
    public Guid Id { get; set; }
    public double AdjustmentFactor { get; set; }
    public string Description { get; set; }
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
    public string? RegionBoundsJson { get; set; } // Store as JSON string for PolygonDTO
    public double? CoverageRadiusKm { get; set; }

    public RegionalParameter()
    {
    }

    public RegionalParameter(Guid id, double adjustmentFactor, string description)
    {
        Id = id;
        AdjustmentFactor = adjustmentFactor;
        Description = description;
    }

    public RegionalParameter(Guid id, double adjustmentFactor, string description, 
        double? centerLatitude, double? centerLongitude, string? regionBoundsJson, double? coverageRadiusKm)
    {
        Id = id;
        AdjustmentFactor = adjustmentFactor;
        Description = description;
        CenterLatitude = centerLatitude;
        CenterLongitude = centerLongitude;
        RegionBoundsJson = regionBoundsJson;
        CoverageRadiusKm = coverageRadiusKm;
    }
}