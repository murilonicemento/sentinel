using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.UnitTests.Domain;

public class GeoPolygonTests
{
    private static GeoPolygon CreateClosedPolygon()
    {
        var points = new List<GeoPoint>
        {
            new(0, 0),
            new(1, 0),
            new(1, 1),
            new(0, 1),
            new(0, 0)
        };
        return new GeoPolygon(points);
    }

    [Fact]
    public void Constructor_WithValidClosedPolygon_CreatesGeoPolygon()
    {
        var polygon = CreateClosedPolygon();

        Assert.NotNull(polygon.Points);
        Assert.Equal(5, polygon.Points.Count);
    }

    [Fact]
    public void Constructor_WithNullPoints_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new GeoPolygon(null!));
        Assert.Equal("points", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithLessThanFourPoints_ThrowsArgumentException()
    {
        var points = new List<GeoPoint>
        {
            new(0, 0),
            new(1, 0),
            new(0, 1)
        };

        var ex = Assert.Throws<ArgumentException>(() => new GeoPolygon(points));
        Assert.Contains("at least 4 points", ex.Message);
    }

    [Fact]
    public void Constructor_WithOpenPolygon_ThrowsArgumentException()
    {
        var points = new List<GeoPoint>
        {
            new(0, 0),
            new(1, 0),
            new(1, 1),
            new(0, 1)
        };

        var ex = Assert.Throws<ArgumentException>(() => new GeoPolygon(points));
        Assert.Contains("closed", ex.Message);
    }

    [Fact]
    public void Constructor_WithMinimumFourPointsClosed_Succeeds()
    {
        var points = new List<GeoPoint>
        {
            new(0, 0),
            new(1, 0),
            new(0, 1),
            new(0, 0)
        };

        var polygon = new GeoPolygon(points);

        Assert.Equal(4, polygon.Points.Count);
    }
}
