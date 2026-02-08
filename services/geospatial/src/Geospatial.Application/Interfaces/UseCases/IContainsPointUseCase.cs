using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IContainsPointUseCase
{
    public bool Execute(GeoPolygon area, GeoPoint point);
}