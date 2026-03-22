using Geospatial.Application.DTOs;
using Geospatial.Application.Mappers;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.UnitTests.Mappers;

public class GeoRadiusMapperTests
{
    [Fact]
    public void ToDomain_WithMetersUnit_ReturnsGeoRadius()
    {
        var dto = new RadiusDTO { Value = 500, Unit = "Meters" };

        var result = GeoRadiusMapper.ToDomain(dto);

        Assert.NotNull(result);
        Assert.Equal(500, result.Value);
        Assert.Equal(DistanceUnit.Meters, result.Unit);
    }

    [Fact]
    public void ToDomain_WithKilometersUnit_ReturnsGeoRadius()
    {
        var dto = new RadiusDTO { Value = 5, Unit = "Kilometers" };

        var result = GeoRadiusMapper.ToDomain(dto);

        Assert.NotNull(result);
        Assert.Equal(5, result.Value);
        Assert.Equal(DistanceUnit.Kilometers, result.Unit);
    }

    [Fact]
    public void ToDomain_WithCaseInsensitiveUnit_ReturnsGeoRadius()
    {
        var dto = new RadiusDTO { Value = 100, Unit = "meters" };

        var result = GeoRadiusMapper.ToDomain(dto);

        Assert.Equal(DistanceUnit.Meters, result.Unit);
    }

    [Fact]
    public void ToDomain_WithNullDto_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GeoRadiusMapper.ToDomain(null!));
    }
}
