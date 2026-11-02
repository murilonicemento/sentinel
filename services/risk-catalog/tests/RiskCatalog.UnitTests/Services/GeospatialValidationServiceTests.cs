using Microsoft.Extensions.Logging;
using Moq;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Application.Services;

namespace RiskCatalog.UnitTests.Services;

public class GeospatialValidationServiceTests
{
    private readonly Mock<IGeospatialClient> _mockGeospatialClient;
    private readonly Mock<ILogger<GeospatialValidationService>> _mockLogger;
    private readonly GeospatialValidationService _service;

    public GeospatialValidationServiceTests()
    {
        _mockGeospatialClient = new Mock<IGeospatialClient>();
        _mockLogger = new Mock<ILogger<GeospatialValidationService>>();
        _service = new GeospatialValidationService(_mockGeospatialClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ValidateCoordinatesAsync_WithValidCoordinates_ReturnsTrue()
    {
        // Arrange
        var latitude = -23.5505;
        var longitude = -46.6333;
        _mockGeospatialClient
            .Setup(x => x.ContainsPointAsync(It.IsAny<PointDTO>(), It.IsAny<PolygonDTO>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ValidateCoordinatesAsync(latitude, longitude);

        // Assert
        Assert.True(result);
        _mockGeospatialClient.Verify(
            x => x.ContainsPointAsync(It.IsAny<PointDTO>(), It.IsAny<PolygonDTO>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateCoordinatesAsync_WithInvalidRange_ReturnsFalse()
    {
        // Arrange
        var latitude = 91.0; // Invalid latitude
        var longitude = -46.6333;

        // Act
        var result = await _service.ValidateCoordinatesAsync(latitude, longitude);

        // Assert
        Assert.False(result);
        _mockGeospatialClient.Verify(
            x => x.ContainsPointAsync(It.IsAny<PointDTO>(), It.IsAny<PolygonDTO>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateRegionBoundsAsync_WithValidBounds_ReturnsTrue()
    {
        // Arrange
        var regionBounds = new PolygonDTO
        {
            Coordinates = new List<PointDTO>
            {
                new() { Latitude = -24.0, Longitude = -47.0 },
                new() { Latitude = -24.0, Longitude = -46.0 },
                new() { Latitude = -23.0, Longitude = -46.0 },
                new() { Latitude = -23.0, Longitude = -47.0 },
                new() { Latitude = -24.0, Longitude = -47.0 }
            }
        };

        _mockGeospatialClient
            .Setup(x => x.IntersectsAsync(It.IsAny<PolygonDTO>(), It.IsAny<PolygonDTO>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ValidateRegionBoundsAsync(regionBounds);

        // Assert
        Assert.True(result);
        _mockGeospatialClient.Verify(
            x => x.IntersectsAsync(It.IsAny<PolygonDTO>(), It.IsAny<PolygonDTO>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CalculateDistanceFromRegionAsync_ReturnsDistanceResult()
    {
        // Arrange
        var point = new PointDTO { Latitude = -23.5505, Longitude = -46.6333 };
        var regionCenter = new PointDTO { Latitude = -23.5500, Longitude = -46.6330 };
        var radius = new RadiusDTO { Value = 10, Unit = "km" };

        var expectedResult = (true, 5.5);
        _mockGeospatialClient
            .Setup(x => x.WithinRadiusAsync(regionCenter, point, radius, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.CalculateDistanceFromRegionAsync(point, regionCenter, radius);

        // Assert
        Assert.Equal(expectedResult.Item1, result.WithinRadius);
        Assert.Equal(expectedResult.Item2, result.Distance);
        _mockGeospatialClient.Verify(
            x => x.WithinRadiusAsync(regionCenter, point, radius, It.IsAny<CancellationToken>()), Times.Once);
    }
}