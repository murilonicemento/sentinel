using Geospatial.Application.DTOs;
using Geospatial.Application.Mappers;

namespace Geospatial.UnitTests.Mappers;

public class GeoPolygonMapperTests
{
    [Fact]
    public void ToDomain_WithValidDto_ReturnsGeoPolygon()
    {
        var dto = new PolygonDTO
        {
            Coordinates = new List<PointDTO>
            {
                new() { Latitude = 0, Longitude = 0 },
                new() { Latitude = 1, Longitude = 0 },
                new() { Latitude = 1, Longitude = 1 },
                new() { Latitude = 0, Longitude = 1 },
                new() { Latitude = 0, Longitude = 0 }
            }
        };

        var result = GeoPolygonMapper.ToDomain(dto);

        Assert.NotNull(result);
        Assert.Equal(5, result.Points.Count);
    }

    [Fact]
    public void ToDomain_WithNullDto_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GeoPolygonMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomain_WithNullCoordinates_ThrowsArgumentException()
    {
        var dto = new PolygonDTO { Coordinates = null! };

        var ex = Assert.Throws<ArgumentException>(() => GeoPolygonMapper.ToDomain(dto));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void ToDomain_WithEmptyCoordinates_ThrowsArgumentException()
    {
        var dto = new PolygonDTO { Coordinates = new List<PointDTO>() };

        var ex = Assert.Throws<ArgumentException>(() => GeoPolygonMapper.ToDomain(dto));
        Assert.Contains("empty", ex.Message);
    }
}
