using Geospatial.Application.Models;

namespace Geospatial.Application.Interfaces.Repositories;

public interface IGeospatialEventRepository
{
    Task IndexAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPointAsync(double lat, double lon, double radiusMeters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(IReadOnlyList<(double Lat, double Lon)> polygon, CancellationToken cancellationToken = default);
}
