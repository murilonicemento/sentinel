using Geospatial.Application.UseCases;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class DistanceUseCaseTests
{
    private readonly Mock<IGeospatialCalculator> _mockCalculator;
    private readonly DistanceUseCase _sut;

    public DistanceUseCaseTests()
    {
        _mockCalculator = new Mock<IGeospatialCalculator>();
        _sut = new DistanceUseCase(_mockCalculator.Object);
    }

    [Fact]
    public void Execute_ReturnsDistanceFromCalculator()
    {
        var from = new GeoPoint(-23.5505, -46.6333);
        var to = new GeoPoint(-23.5506, -46.6334);
        const double expectedDistance = 150.5;

        _mockCalculator.Setup(x => x.Distance(from, to)).Returns(expectedDistance);

        var result = _sut.Execute(from, to);

        Assert.Equal(expectedDistance, result);
        _mockCalculator.Verify(x => x.Distance(from, to), Times.Once);
    }

    [Fact]
    public void Execute_WhenSamePoint_ReturnsZero()
    {
        var point = new GeoPoint(-23.5505, -46.6333);

        _mockCalculator.Setup(x => x.Distance(point, point)).Returns(0);

        var result = _sut.Execute(point, point);

        Assert.Equal(0, result);
    }
}
