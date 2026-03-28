using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.IntegrationClients;

/// <summary>
/// Client for integrating with Geospatial service for spatial context.
/// </summary>
public interface IGeospatialClient
{
    public Task<bool> IsPointInRiskAreaAsync(int latitude, int longitude, string areaType, CancellationToken cancellationToken = default);
    public Task<GeospatialContextDto?> GetSpatialContextAsync(int latitude, int longitude, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<NearbyRiskDto>> GetNearbyRisksAsync(int latitude, int longitude, double radiusMeters, CancellationToken cancellationToken = default);
    public Task<double> CalculateDistanceAsync(int lat1, int lon1, int lat2, int lon2, CancellationToken cancellationToken = default);
}

public class GeospatialContextDto
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public string? RegionId { get; set; }
    public string? TerrainType { get; set; }
    public double Elevation { get; set; }
    public double ProximityToCoast { get; set; }
    public List<string> RiskZones { get; set; } = new();
}

public class NearbyRiskDto
{
    public string RiskType { get; set; } = string.Empty;
    public double Distance { get; set; }
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public double Severity { get; set; }
}
