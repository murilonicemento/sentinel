using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace Geospatial.Infrastructure.GeometryEngine;

public static class GeometryConverters
{
    public static Point ToPoint(GeoPoint point, GeometryFactory factory)
        => factory.CreatePoint(new Coordinate(point.Longitude, point.Latitude));

    public static Polygon ToPolygon(GeoPolygon polygon, GeometryFactory factory)
    {
        var coords = polygon.Points
            .Select(p => new Coordinate(p.Longitude, p.Latitude))
            .ToArray();

        if (!coords.First().Equals2D(coords.Last()))
        {
            var list = coords.ToList();
            list.Add(list.First());
            coords = list.ToArray();
        }

        var linearRing = factory.CreateLinearRing(coords);

        return factory.CreatePolygon(linearRing);
    }
}