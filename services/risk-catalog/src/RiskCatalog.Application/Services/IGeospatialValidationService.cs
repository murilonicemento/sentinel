using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Services;

public interface IGeospatialValidationService
{
    Task<bool> ValidateCoordinatesAsync(double latitude, double longitude,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateRegionBoundsAsync(PolygonDTO regionBounds, CancellationToken cancellationToken = default);

    Task<(bool WithinRadius, double Distance)> CalculateDistanceFromRegionAsync(
        PointDTO point,
        PointDTO regionCenter,
        RadiusDTO radius,
        CancellationToken cancellationToken = default);
}