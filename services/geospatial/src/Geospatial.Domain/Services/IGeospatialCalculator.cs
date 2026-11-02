using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Domain.Services;

public interface IGeospatialCalculator
{
    public bool ContainsPoint(GeoPolygon area, GeoPoint point);
    public bool Intersects(GeoPolygon first, GeoPolygon second);
    public double Distance(GeoPoint from, GeoPoint to);
}