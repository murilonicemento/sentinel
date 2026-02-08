using Geospatial.Application.UseCases;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class ContainsPointUseCaseTests
{
    private readonly Mock<IGeospatialCalculator> _mockCalculator;
    private readonly ContainsPointUseCase _sut;

    public ContainsPointUseCaseTests()
    {
        _mockCalculator = new Mock<IGeospatialCalculator>();
        _sut = new ContainsPointUseCase(_mockCalculator.Object);
    }

    [Fact]
    public void Execute_WhenPointIsInside_ReturnsTrue()
    {
        var area = CreateTestPolygon();
        var point = new GeoPoint(0.5, 0.5);

        _mockCalculator.Setup(x => x.ContainsPoint(area, point)).Returns(true);

        var result = _sut.Execute(area, point);

        Assert.True(result);
        _mockCalculator.Verify(x => x.ContainsPoint(area, point), Times.Once);
    }

    [Fact]
    public void Execute_WhenPointIsOutside_ReturnsFalse()
    {
        var area = CreateTestPolygon();
        var point = new GeoPoint(2, 2);

        _mockCalculator.Setup(x => x.ContainsPoint(area, point)).Returns(false);

        var result = _sut.Execute(area, point);

        Assert.False(result);
        _mockCalculator.Verify(x => x.ContainsPoint(area, point), Times.Once);
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
