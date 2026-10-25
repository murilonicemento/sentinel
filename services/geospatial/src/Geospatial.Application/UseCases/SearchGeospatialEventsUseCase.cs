using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;

namespace Geospatial.Application.UseCases;

public class SearchGeospatialEventsUseCase : ISearchGeospatialEventsUseCase
{
    private readonly IGeospatialEventRepository _eventRepository;

    public SearchGeospatialEventsUseCase(IGeospatialEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByRadiusAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
        => _eventRepository.SearchByPointAsync(latitude, longitude, radiusMeters, cancellationToken);

    public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(
        IReadOnlyList<(double Latitude, double Longitude)> polygon,
        CancellationToken cancellationToken = default)
        => _eventRepository.SearchByPolygonAsync(
            polygon.Select(p => (p.Latitude, p.Longitude)).ToList(),
            cancellationToken);
}
