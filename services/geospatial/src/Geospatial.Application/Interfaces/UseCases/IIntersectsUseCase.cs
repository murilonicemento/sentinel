using Geospatial.Domain.Geometry;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IIntersectsUseCase
{
    public bool Execute(GeoPolygon a, GeoPolygon b);
}