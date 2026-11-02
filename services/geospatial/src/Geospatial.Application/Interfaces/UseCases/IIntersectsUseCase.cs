using Geospatial.Domain.Geometry;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IIntersectsUseCase
{
    Task<bool> ExecuteAsync(GeoPolygon a, GeoPolygon b, CancellationToken cancellationToken = default);
}