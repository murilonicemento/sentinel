using Geospatial.Domain.Events;

namespace Geospatial.Application.Interfaces.UseCases;

public interface ISearchGeospatialEventsUseCase
{
    Task<IReadOnlyList<GeospatialOperationEvent>> SearchByRadiusAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(
        IReadOnlyList<(double Latitude, double Longitude)> polygon,
        CancellationToken cancellationToken = default);
}
