using System.Text.Json;
using Geospatial.Application.DTOs;
using Geospatial.Application.Mappers;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.UnitTests.Mappers;

public class BatchRequestMapperTests
{
    [Fact]
    public void ToDomain_WithContainsPointEvaluation_MapsCorrectly()
    {
        var payload = new ContainsPointPayloadDTO
        {
            Area = new PolygonDTO
            {
                Coordinates = new List<PointDTO>
                {
                    new() { Latitude = 0, Longitude = 0 },
                    new() { Latitude = 1, Longitude = 0 },
                    new() { Latitude = 1, Longitude = 1 },
                    new() { Latitude = 0, Longitude = 1 },
                    new() { Latitude = 0, Longitude = 0 }
                }
            },
            Point = new PointDTO { Latitude = 0.5, Longitude = 0.5 }
        };
        var dto = new BatchRequestDTO
        {
            Evaluations = new List<BatchEvaluationDTO>
            {
                new()
                {
                    Type = "contains-point",
                    Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload))
                }
            }
        };

        var result = BatchRequestMapper.ToDomain(dto);

        Assert.Single(result);
        Assert.Equal("contains-point", result[0].Type, StringComparer.OrdinalIgnoreCase);
        var (area, point) = ((GeoPolygon, GeoPoint))result[0].DomainPayload;
        Assert.Equal(5, area.Points.Count);
        Assert.Equal(0.5, point.Latitude);
        Assert.Equal(0.5, point.Longitude);
    }

    [Fact]
    public void ToDomain_WithWithinRadiusEvaluation_MapsCorrectly()
    {
        var payload = new WithinRadiusPayloadDTO
        {
            Center = new PointDTO { Latitude = -23.5505, Longitude = -46.6333 },
            Point = new PointDTO { Latitude = -23.5506, Longitude = -46.6334 },
            Radius = new RadiusDTO { Value = 500, Unit = "Meters" }
        };
        var dto = new BatchRequestDTO
        {
            Evaluations = new List<BatchEvaluationDTO>
            {
                new()
                {
                    Type = "within-radius",
                    Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload))
                }
            }
        };

        var result = BatchRequestMapper.ToDomain(dto);

        Assert.Single(result);
        Assert.Equal("within-radius", result[0].Type, StringComparer.OrdinalIgnoreCase);
        var (center, target, radius) = ((GeoPoint, GeoPoint, GeoRadius))result[0].DomainPayload;
        Assert.Equal(-23.5505, center.Latitude);
        Assert.Equal(500, radius.Value);
    }

    [Fact]
    public void ToDomain_WithUnknownType_ThrowsArgumentException()
    {
        var dto = new BatchRequestDTO
        {
            Evaluations = new List<BatchEvaluationDTO>
            {
                new()
                {
                    Type = "invalid-type",
                    Payload = JsonSerializer.Deserialize<JsonElement>("{}")
                }
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => BatchRequestMapper.ToDomain(dto));
        Assert.Contains("not supported", ex.Message);
    }
}