using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.UseCases;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class ContainsPointUseCaseTests
{
    private readonly Mock<IGeospatialCalculator> _mockCalculator;
    private readonly Mock<IGeospatialEventRepository> _mockEventRepository;
    private readonly Mock<ILogger<ContainsPointUseCase>> _mockLogger;
    private readonly ContainsPointUseCase _sut;

    public ContainsPointUseCaseTests()
    {
        _mockCalculator = new Mock<IGeospatialCalculator>();
        _mockEventRepository = new Mock<IGeospatialEventRepository>();
        _mockEventRepository.Setup(x => x.IndexAsync(It.IsAny<Geospatial.Application.Models.GeospatialOperationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockLogger = new Mock<ILogger<ContainsPointUseCase>>();
        _sut = new ContainsPointUseCase(_mockCalculator.Object, _mockEventRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPointIsInside_ReturnsTrue()
    {
        var area = CreateTestPolygon();
        var point = new GeoPoint(0.5, 0.5);

        _mockCalculator.Setup(x => x.ContainsPoint(area, point)).Returns(true);

        var result = await _sut.ExecuteAsync(area, point);

        Assert.True(result);
        _mockCalculator.Verify(x => x.ContainsPoint(area, point), Times.Once);
        _mockEventRepository.Verify(x => x.IndexAsync(It.IsAny<Geospatial.Application.Models.GeospatialOperationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPointIsOutside_ReturnsFalse()
    {
        var area = CreateTestPolygon();
        var point = new GeoPoint(2, 2);

        _mockCalculator.Setup(x => x.ContainsPoint(area, point)).Returns(false);

        var result = await _sut.ExecuteAsync(area, point);

        Assert.False(result);
        _mockCalculator.Verify(x => x.ContainsPoint(area, point), Times.Once);
        _mockEventRepository.Verify(x => x.IndexAsync(It.IsAny<Geospatial.Application.Models.GeospatialOperationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
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
}
