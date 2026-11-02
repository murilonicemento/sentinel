using Geospatial.Domain.Events;

namespace Geospatial.Domain.Repositories;

public interface IGeospatialEventRepository
{
    public Task IndexAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPointAsync(double lat, double lon, double radiusMeters, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(IReadOnlyList<(double Lat, double Lon)> polygon, CancellationToken cancellationToken = default);
}
