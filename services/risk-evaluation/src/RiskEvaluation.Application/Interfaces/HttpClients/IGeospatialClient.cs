namespace RiskEvaluation.Application.Interfaces.HttpClients;

/// <summary>
/// Client for integrating with Geospatial service for spatial context.
/// </summary>
public interface IGeospatialClient
{
    public Task<bool> IsPointInRiskAreaAsync(int latitude, int longitude, string areaType, CancellationToken cancellationToken = default);
    public Task<GeospatialContextDTO?> GetSpatialContextAsync(int latitude, int longitude, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<NearbyRiskDTO>> GetNearbyRisksAsync(int latitude, int longitude, double radiusMeters, CancellationToken cancellationToken = default);
    public Task<double> CalculateDistanceAsync(int lat1, int lon1, int lat2, int lon2, CancellationToken cancellationToken = default);
}
