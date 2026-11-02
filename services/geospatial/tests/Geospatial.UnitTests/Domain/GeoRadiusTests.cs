using Geospatial.Domain.ValueObjects;

namespace Geospatial.UnitTests.Domain;

public class GeoRadiusTests
{
    [Theory]
    [InlineData(100, DistanceUnit.Meters, 100)]
    [InlineData(1, DistanceUnit.Kilometers, 1000)]
    [InlineData(5.5, DistanceUnit.Kilometers, 5500)]
    public void ToMeters_WithValidUnit_ReturnsCorrectValue(double value, DistanceUnit unit, double expectedMeters)
    {
        var radius = new GeoRadius(value, unit);

        Assert.Equal(expectedMeters, radius.ToMeters());
    }

    [Fact]
    public void Constructor_WithValidValue_CreatesGeoRadius()
    {
        var radius = new GeoRadius(500, DistanceUnit.Meters);

        Assert.Equal(500, radius.Value);
        Assert.Equal(DistanceUnit.Meters, radius.Unit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithZeroOrNegativeValue_ThrowsArgumentOutOfRangeException(double value)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoRadius(value, DistanceUnit.Meters));
        Assert.Equal("value", ex.ParamName);
        Assert.Contains("Radius must be greater than zero", ex.Message);
    }
}
