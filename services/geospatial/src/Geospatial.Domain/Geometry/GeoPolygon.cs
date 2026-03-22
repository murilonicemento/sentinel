using Geospatial.Domain.ValueObjects;

namespace Geospatial.Domain.Geometry;

public class GeoPolygon : IGeometry
{
    public GeometryType Type { get; }
    public IReadOnlyCollection<GeoPoint> Points { get; }

    public GeoPolygon(IEnumerable<GeoPoint> points)
    {
        var pointList = points?.ToList() ?? throw new ArgumentNullException(nameof(points));

        if (pointList.Count < 4)
            throw new ArgumentException("Polygon must have at least 4 points (including closure)");
        if (!IsClosed(pointList))
            throw new ArgumentException("Polygon must be closed (first and last points must be equal)");

        Points = pointList.AsReadOnly();
    }

    private static bool IsClosed(IReadOnlyList<GeoPoint> points)
    {
        var first = points[0];
        var last = points[^1];

        return first.Latitude == last.Latitude && first.Longitude == last.Longitude;
    }
}