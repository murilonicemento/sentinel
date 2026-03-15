using Geospatial.Application.UseCases;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class DistanceUseCaseTests
{
    private readonly Mock<IGeospatialCalculator> _mockCalculator;
    private readonly Mock<IGeospatialEventRepository> _mockEventRepository;
    private readonly Mock<ILogger<DistanceUseCase>> _mockLogger;
    private readonly DistanceUseCase _sut;

    public DistanceUseCaseTests()
    {
        _mockCalculator = new Mock<IGeospatialCalculator>();
        _mockEventRepository = new Mock<IGeospatialEventRepository>();
        _mockEventRepository.Setup(x => x.IndexAsync(It.IsAny<GeospatialOperationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockLogger = new Mock<ILogger<DistanceUseCase>>();
        _sut = new DistanceUseCase(_mockCalculator.Object, _mockEventRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDistanceFromCalculator()
    {
        var from = new GeoPoint(-23.5505, -46.6333);
        var to = new GeoPoint(-23.5506, -46.6334);
        const double expectedDistance = 150.5;

        _mockCalculator.Setup(x => x.Distance(from, to)).Returns(expectedDistance);

        var result = await _sut.ExecuteAsync(from, to);

        Assert.Equal(expectedDistance, result);
        _mockCalculator.Verify(x => x.Distance(from, to), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSamePoint_ReturnsZero()
    {
        var point = new GeoPoint(-23.5505, -46.6333);

        _mockCalculator.Setup(x => x.Distance(point, point)).Returns(0);

        var result = await _sut.ExecuteAsync(point, point);

        Assert.Equal(0, result);
    }
}
