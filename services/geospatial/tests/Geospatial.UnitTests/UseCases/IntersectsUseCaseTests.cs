using Geospatial.Application.UseCases;
using Geospatial.Domain.Events;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.Repositories;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class IntersectsUseCaseTests
{
    private readonly Mock<IGeospatialCalculator> _mockCalculator;
    private readonly Mock<IGeospatialEventRepository> _mockEventRepository;
    private readonly Mock<ILogger<IntersectsUseCase>> _mockLogger;
    private readonly IntersectsUseCase _sut;

    public IntersectsUseCaseTests()
    {
        _mockCalculator = new Mock<IGeospatialCalculator>();
        _mockEventRepository = new Mock<IGeospatialEventRepository>();
        _mockEventRepository.Setup(x => x.IndexAsync(It.IsAny<GeospatialOperationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockLogger = new Mock<ILogger<IntersectsUseCase>>();
        _sut = new IntersectsUseCase(_mockCalculator.Object, _mockEventRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolygonsIntersect_ReturnsTrue()
    {
        var polyA = CreateTestPolygon();
        var polyB = CreateOverlappingPolygon();

        _mockCalculator.Setup(x => x.Intersects(polyA, polyB)).Returns(true);

        var result = await _sut.ExecuteAsync(polyA, polyB);

        Assert.True(result);
        _mockCalculator.Verify(x => x.Intersects(polyA, polyB), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolygonsDoNotIntersect_ReturnsFalse()
    {
        var polyA = CreateTestPolygon();
        var polyB = CreateNonOverlappingPolygon();

        _mockCalculator.Setup(x => x.Intersects(polyA, polyB)).Returns(false);

        var result = await _sut.ExecuteAsync(polyA, polyB);

        Assert.False(result);
    }

    private static GeoPolygon CreateTestPolygon()
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

    private static GeoPolygon CreateOverlappingPolygon()
    {
        var points = new List<GeoPoint>
        {
            new(0.5, 0.5),
            new(1.5, 0.5),
            new(1.5, 1.5),
            new(0.5, 1.5),
            new(0.5, 0.5)
        };
        return new GeoPolygon(points);
    }

    private static GeoPolygon CreateNonOverlappingPolygon()
    {
        var points = new List<GeoPoint>
        {
            new(10, 10),
            new(11, 10),
            new(11, 11),
            new(10, 11),
            new(10, 10)
        };
        return new GeoPolygon(points);
    }
}
