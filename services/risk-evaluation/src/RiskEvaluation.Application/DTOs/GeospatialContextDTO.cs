namespace RiskEvaluation.Application.DTOs;

public class GeospatialContextDTO
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public string? RegionId { get; set; }
    public string? TerrainType { get; set; }
    public double Elevation { get; set; }
    public double ProximityToCoast { get; set; }
    public List<string> RiskZones { get; set; } = [];
}
