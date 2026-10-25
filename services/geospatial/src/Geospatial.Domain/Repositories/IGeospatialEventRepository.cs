using Geospatial.Domain.Events;

namespace Geospatial.Domain.Repositories;

public interface IGeospatialEventRepository
{
    public Task IndexAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPointAsync(double lat, double lon, double radiusMeters, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(IReadOnlyList<(double Lat, double Lon)> polygon, CancellationToken cancellationToken = default);

    /// <summary>Pages events within the radius, newest first.</summary>
    public Task<GeospatialEventPage> SearchByPointAsync(double lat, double lon, double radiusMeters, int skip, int take,
        CancellationToken cancellationToken = default);

    /// <summary>Pages events inside the polygon, newest first.</summary>
    public Task<GeospatialEventPage> SearchByPolygonAsync(IReadOnlyList<(double Lat, double Lon)> polygon, int skip,
        int take, CancellationToken cancellationToken = default);
}

public sealed record GeospatialEventPage(IReadOnlyList<GeospatialOperationEvent> Items, long TotalItems);
