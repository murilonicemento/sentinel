namespace RiskEvaluation.Domain.Services;

/// <summary>
/// Geospatial context for risk calculation adjustments.
/// </summary>
public class GeospatialContext
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public string? RegionId { get; set; }
    public string? TerrainType { get; set; }
    public double Elevation { get; set; }
    public double ProximityToCoast { get; set; }
    public List<string> RiskZones { get; set; } = [];
}
