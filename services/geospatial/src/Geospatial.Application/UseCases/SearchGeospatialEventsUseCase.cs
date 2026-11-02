using Geospatial.Application.DTOs;
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

    public async Task<PagedResultDTO<GeospatialEventDTO>> SearchByRadiusAsync(double latitude, double longitude,
        double radiusMeters, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await _eventRepository.SearchByPointAsync(latitude, longitude, radiusMeters,
            (page - 1) * pageSize, pageSize, cancellationToken);
        return ToPagedResult(result, page, pageSize);
    }

    public async Task<PagedResultDTO<GeospatialEventDTO>> SearchByPolygonAsync(
        IReadOnlyList<(double Latitude, double Longitude)> polygon, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _eventRepository.SearchByPolygonAsync(
            polygon.Select(p => (p.Latitude, p.Longitude)).ToList(), (page - 1) * pageSize, pageSize,
            cancellationToken);
        return ToPagedResult(result, page, pageSize);
    }

    private static PagedResultDTO<GeospatialEventDTO> ToPagedResult(GeospatialEventPage result, int page,
        int pageSize) => new()
        {
            Items = result.Items.Select(GeospatialEventDTO.From).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = result.TotalItems,
            TotalPages = (int)Math.Ceiling(result.TotalItems / (double)pageSize)
        };
}
