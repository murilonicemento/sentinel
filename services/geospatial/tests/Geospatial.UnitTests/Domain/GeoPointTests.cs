using Geospatial.Domain.ValueObjects;

namespace Geospatial.UnitTests.Domain;

public class GeoPointTests
{
    [Fact]
    public void Constructor_WithValidCoordinates_CreatesGeoPoint()
    {
        var point = new GeoPoint(-23.5505, -46.6333);

        Assert.Equal(-23.5505, point.Latitude);
        Assert.Equal(-46.6333, point.Longitude);
    }

    [Theory]
    [InlineData(-90, 0)]
    [InlineData(90, 0)]
    [InlineData(0, -180)]
    [InlineData(0, 180)]
    public void Constructor_WithBoundaryCoordinates_CreatesGeoPoint(double lat, double lon)
    {
        var point = new GeoPoint(lat, lon);

        Assert.Equal(lat, point.Latitude);
        Assert.Equal(lon, point.Longitude);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(-100, 0)]
    [InlineData(100, 0)]
    public void Constructor_WithInvalidLatitude_ThrowsArgumentOutOfRangeException(double lat, double lon)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(lat, lon));
        Assert.Equal("latitude", ex.ParamName);
        Assert.Contains("Latitude must be between -90 and 90", ex.Message);
    }

    [Theory]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    [InlineData(0, -200)]
    [InlineData(0, 200)]
    public void Constructor_WithInvalidLongitude_ThrowsArgumentOutOfRangeException(double lat, double lon)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(lat, lon));
        Assert.Equal("longitude", ex.ParamName);
        Assert.Contains("Longitude must be between -180 and 180", ex.Message);
    }
}
