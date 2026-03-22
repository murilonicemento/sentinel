using System.Text.Json;
using Geospatial.Application.DTOs;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.UseCases;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class BatchEvaluateUseCaseTests
{
    private readonly Mock<IContainsPointUseCase> _mockContainsPointUseCase;
    private readonly Mock<IWithinRadiusUseCase> _mockWithinRadiusUseCase;
    private readonly Mock<ILogger<BatchEvaluateUseCase>> _mockLogger;
    private readonly BatchEvaluateUseCase _sut;

    public BatchEvaluateUseCaseTests()
    {
        _mockContainsPointUseCase = new Mock<IContainsPointUseCase>();
        _mockWithinRadiusUseCase = new Mock<IWithinRadiusUseCase>();
        _mockLogger = new Mock<ILogger<BatchEvaluateUseCase>>();
        _sut = new BatchEvaluateUseCase(_mockContainsPointUseCase.Object, _mockWithinRadiusUseCase.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithContainsPointEvaluation_ReturnsCorrectResult()
    {
        var request = CreateContainsPointRequest();
        _mockContainsPointUseCase.Setup(x => x.ExecuteAsync(It.IsAny<GeoPolygon>(), It.IsAny<GeoPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(request);

        Assert.Single(result.Results);
        Assert.Equal("contains-point", result.Results[0].Type, StringComparer.OrdinalIgnoreCase);
        Assert.True(result.Results[0].Contains);
    }

    [Fact]
    public async Task ExecuteAsync_WithWithinRadiusEvaluation_ReturnsCorrectResult()
    {
        var request = CreateWithinRadiusRequest();
        _mockWithinRadiusUseCase.Setup(x => x.ExecuteAsync(It.IsAny<GeoPoint>(), It.IsAny<GeoPoint>(), It.IsAny<GeoRadius>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 250.0));

        var result = await _sut.ExecuteAsync(request);

        Assert.Single(result.Results);
        Assert.Equal("within-radius", result.Results[0].Type, StringComparer.OrdinalIgnoreCase);
        Assert.True(result.Results[0].WithinRadius);
        Assert.Equal(250.0, result.Results[0].Distance);
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedEvaluations_ReturnsAllResults()
    {
        var request = new BatchRequestDTO
        {
            Evaluations = new List<BatchEvaluationDTO>
            {
                CreateContainsPointEval(),
                CreateWithinRadiusEval()
            }
        };

        _mockContainsPointUseCase.Setup(x => x.ExecuteAsync(It.IsAny<GeoPolygon>(), It.IsAny<GeoPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockWithinRadiusUseCase.Setup(x => x.ExecuteAsync(It.IsAny<GeoPoint>(), It.IsAny<GeoPoint>(), It.IsAny<GeoRadius>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 1500.0));

        var result = await _sut.ExecuteAsync(request);

        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].Contains);
        Assert.False(result.Results[1].WithinRadius);
        Assert.Equal(1500.0, result.Results[1].Distance);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownEvaluationType_ThrowsArgumentException()
    {
        var request = new BatchRequestDTO
        {
            Evaluations = new List<BatchEvaluationDTO>
            {
                new()
                {
                    Type = "unknown-type",
                    Payload = JsonSerializer.Deserialize<JsonElement>("{}")
                }
            }
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.ExecuteAsync(request));
        Assert.Contains("not supported", ex.Message);
    }

    private static BatchRequestDTO CreateContainsPointRequest()
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
        return new BatchRequestDTO
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
    }

    private static BatchEvaluationDTO CreateContainsPointEval()
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
        return new BatchEvaluationDTO
        {
            Type = "contains-point",
            Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload))
        };
    }

    private static BatchRequestDTO CreateWithinRadiusRequest()
    {
        var payload = new WithinRadiusPayloadDTO
        {
            Center = new PointDTO { Latitude = -23.5505, Longitude = -46.6333 },
            Point = new PointDTO { Latitude = -23.5506, Longitude = -46.6334 },
            Radius = new RadiusDTO { Value = 500, Unit = "Meters" }
        };
        return new BatchRequestDTO
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
    }

    private static BatchEvaluationDTO CreateWithinRadiusEval()
    {
        var payload = new WithinRadiusPayloadDTO
        {
            Center = new PointDTO { Latitude = -23.5505, Longitude = -46.6333 },
            Point = new PointDTO { Latitude = -23.5506, Longitude = -46.6334 },
            Radius = new RadiusDTO { Value = 500, Unit = "Meters" }
        };
        return new BatchEvaluationDTO
        {
            Type = "within-radius",
            Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload))
        };
    }
}
