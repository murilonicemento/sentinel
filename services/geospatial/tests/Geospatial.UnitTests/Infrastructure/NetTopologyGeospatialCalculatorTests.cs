using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;
using Geospatial.Infrastructure.GeometryEngine;

namespace Geospatial.UnitTests.Infrastructure;

public class NetTopologyGeospatialCalculatorTests
{
    private readonly NetTopologyGeospatialCalculator _sut = new();

    [Fact]
    public void ContainsPoint_WhenPointInsidePolygon_ReturnsTrue()
    {
        var polygon = CreateSquarePolygon(0, 0, 1, 1);
        var point = new GeoPoint(0.5, 0.5);

        var result = _sut.ContainsPoint(polygon, point);

        Assert.True(result);
    }

    [Fact]
    public void ContainsPoint_WhenPointOutsidePolygon_ReturnsFalse()
    {
        var polygon = CreateSquarePolygon(0, 0, 1, 1);
        var point = new GeoPoint(2, 2);

        var result = _sut.ContainsPoint(polygon, point);

        Assert.False(result);
    }

    [Fact]
    public void ContainsPoint_WhenPointJustInsidePolygon_ReturnsTrue()
    {
        var polygon = CreateSquarePolygon(0, 0, 1, 1);
        var point = new GeoPoint(0.5, 0.001);

        var result = _sut.ContainsPoint(polygon, point);

        Assert.True(result);
    }

    [Fact]
    public void Intersects_WhenPolygonsOverlap_ReturnsTrue()
    {
        var polyA = CreateSquarePolygon(0, 0, 1, 1);
        var polyB = CreateSquarePolygon(0.5, 0.5, 1.5, 1.5);

        var result = _sut.Intersects(polyA, polyB);

        Assert.True(result);
    }

    [Fact]
    public void Intersects_WhenPolygonsDoNotOverlap_ReturnsFalse()
    {
        var polyA = CreateSquarePolygon(0, 0, 1, 1);
        var polyB = CreateSquarePolygon(10, 10, 11, 11);

        var result = _sut.Intersects(polyA, polyB);

        Assert.False(result);
    }

    [Fact]
    public void Distance_BetweenTwoPoints_ReturnsPositiveValue()
    {
        var from = new GeoPoint(-23.5505, -46.6333);
        var to = new GeoPoint(-23.5506, -46.6334);

        var result = _sut.Distance(from, to);

        Assert.True(result > 0);
    }

    [Fact]
    public void Distance_BetweenSamePoint_ReturnsZero()
    {
        var point = new GeoPoint(-23.5505, -46.6333);

        var result = _sut.Distance(point, point);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Distance_ReturnsApproximateMeters()
    {
        // São Paulo approximate coordinates - distance to a point ~1km south should be ~1000m
        var from = new GeoPoint(-23.5505, -46.6333);
        var to = new GeoPoint(-23.5595, -46.6333);

        var result = _sut.Distance(from, to);

        Assert.True(result > 900 && result < 1200);
    }

    private static GeoPolygon CreateSquarePolygon(double minX, double minY, double maxX, double maxY)
    {
        var points = new List<GeoPoint>
        {
            new(minY, minX),
            new(minY, maxX),
            new(maxY, maxX),
            new(maxY, minX),
            new(minY, minX)
        };
        return new GeoPolygon(points);
    }
}
