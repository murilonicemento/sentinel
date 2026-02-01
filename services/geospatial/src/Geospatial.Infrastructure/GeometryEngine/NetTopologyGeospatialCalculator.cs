using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace Geospatial.Infrastructure.GeometryEngine;

public class NetTopologyGeospatialCalculator : IGeospatialCalculator
{
    private readonly GeometryFactory _geometryFactory;

    public NetTopologyGeospatialCalculator()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
    }

    public bool ContainsPoint(GeoPolygon area, GeoPoint point)
    {
        var polygon = GeometryConverters.ToPolygon(area, _geometryFactory);
        var ntPoint = GeometryConverters.ToPoint(point, _geometryFactory);

        return polygon.Contains(ntPoint);
    }

    public bool Intersects(GeoPolygon first, GeoPolygon second)
    {
        var polyA = GeometryConverters.ToPolygon(first, _geometryFactory);
        var polyB = GeometryConverters.ToPolygon(second, _geometryFactory);

        return polyA.Intersects(polyB);
    }

    public double Distance(GeoPoint from, GeoPoint to)
    {
        var p1 = GeometryConverters.ToPoint(from, _geometryFactory);
        var p2 = GeometryConverters.ToPoint(to, _geometryFactory);

        return p1.Distance(p2) * 111319.9;
    }
}