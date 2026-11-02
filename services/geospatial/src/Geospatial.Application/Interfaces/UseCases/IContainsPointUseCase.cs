using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IContainsPointUseCase
{
    Task<bool> ExecuteAsync(GeoPolygon area, GeoPoint point, CancellationToken cancellationToken = default);
}