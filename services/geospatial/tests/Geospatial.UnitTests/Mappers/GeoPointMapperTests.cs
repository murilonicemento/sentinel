using Geospatial.Application.DTOs;
using Geospatial.Application.Mappers;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.UnitTests.Mappers;

public class GeoPointMapperTests
{
    [Fact]
    public void ToDomain_WithValidDto_ReturnsGeoPoint()
    {
        var dto = new PointDTO { Latitude = -23.5505, Longitude = -46.6333 };

        var result = GeoPointMapper.ToDomain(dto);

        Assert.NotNull(result);
        Assert.Equal(-23.5505, result.Latitude);
        Assert.Equal(-46.6333, result.Longitude);
    }

    [Fact]
    public void ToDomain_WithNullDto_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => GeoPointMapper.ToDomain(null!));
        Assert.Equal("dto", ex.ParamName);
    }

    [Fact]
    public void ToDomain_WithInvalidLatitude_ThrowsArgumentOutOfRangeException()
    {
        var dto = new PointDTO { Latitude = 100, Longitude = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => GeoPointMapper.ToDomain(dto));
    }
}
